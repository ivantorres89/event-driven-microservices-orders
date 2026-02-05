using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderAccept.Api;
using OrderAccept.Persistence.Impl;
using Microsoft.EntityFrameworkCore;

namespace OrderAccept.IntegrationTests.Fixtures;

public sealed class OrderAcceptApiFixture : IAsyncLifetime
{   
    private bool _useLocalInfraResolved;
    private readonly IConfiguration _configuration;
    
    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public string RedisConnectionString =>
        _configuration.GetConnectionString("Redis") ?? "localhost:6379";

    public string RabbitConnectionString =>
        // Allow either ConnectionStrings:RabbitMq (common across IT projects) or RabbitMQ:ConnectionString.
        _configuration.GetConnectionString("RabbitMq")
        ?? _configuration["RabbitMQ:ConnectionString"]
        ?? "amqp://guest:guest@localhost:5672/";

    public string RabbitQueueName =>
        _configuration["RabbitMQ:OutboundQueueName"]
        ?? "order.accepted.it";

    // Dedicated DB for integration tests (keeps local dev DB clean).
    // Resolved in this order:
    // 1) CONTOSO_IT_SQL_CONNECTIONSTRING (full)
    // 2) CONTOSO_IT_SQL_BASE (base without Database=, used in GitHub Actions)
    // 3) ConnectionStrings:Contoso (from tests appsettings.json)
    // 4) SA_PASSWORD (env or infra/local/.env)
    // 5) docker-compose default: Your_strong_Password123!
    public string ContosoConnectionString => ResolveContosoConnectionString(_configuration);

    // Symmetric key used by the test host for JwtBearer validation (dev/demo mode).
    public string JwtSigningKey => "dev-it-signing-key-please-change-123456";



    public OrderAcceptApiFixture()
    {
        _configuration = BuildConfiguration();
    }

    public async Task InitializeAsync()
    {
        _useLocalInfraResolved = IsLocalInfraAvailable();

        if (!_useLocalInfraResolved)
        {
            throw new InvalidOperationException("Local infra not detected. Run 'docker compose up -d' in infra/local before running integration tests.");
        }

        Factory = new CustomWebApplicationFactory(this);

        // Warm up host & ensure the test database schema exists.
        _ = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContosoDbContext>();
        await db.Database.EnsureCreatedAsync();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            Factory.Dispose();

        await Task.CompletedTask;
    }

    private static bool IsLocalInfraAvailable()
    {
        return IsPortOpen("localhost", 6379) && IsPortOpen("localhost", 5672) && IsPortOpen("localhost", 1433);
    }

    private static bool IsPortOpen(string host, int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            return connectTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            return false;
        }
    }

    private sealed class CustomWebApplicationFactory : WebApplicationFactory<OrderAccept.Api.Program>
    {
        private readonly OrderAcceptApiFixture _fixture;

        public CustomWebApplicationFactory(OrderAcceptApiFixture fixture) => _fixture = fixture;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Redis"] = _fixture.RedisConnectionString,
                    ["ConnectionStrings:Contoso"] = _fixture.ContosoConnectionString,
                    ["RabbitMQ:ConnectionString"] = _fixture.RabbitConnectionString,
                    ["RabbitMQ:OutboundQueueName"] = _fixture.RabbitQueueName,
                    ["Jwt:SigningKey"] = _fixture.JwtSigningKey,
                    ["OpenTelemetry:Enabled"] = "false"
                };

                config.AddInMemoryCollection(settings);
            });
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var env =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        // Load a local infra env file if present (infra/local/.env), so devs don't need to export SA_PASSWORD.
        var repoRoot = TryFindRepoRoot(AppContext.BaseDirectory);
        var infraEnvFile = repoRoot is null
            ? null
            : Path.Combine(repoRoot, "infra", "local", ".env");

        if (!string.IsNullOrWhiteSpace(infraEnvFile) && File.Exists(infraEnvFile))
        {
            foreach (var (k, v) in ReadDotEnvFile(infraEnvFile))
            {
                if (string.IsNullOrWhiteSpace(k) || v is null)
                    continue;
                // Don't overwrite explicitly-set environment variables.
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(k)))
                    Environment.SetEnvironmentVariable(k, v);
            }
        }

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveContosoConnectionString(IConfiguration configuration)
    {
        // Highest precedence: full connection string override.
        var full = Environment.GetEnvironmentVariable("CONTOSO_IT_SQL_CONNECTIONSTRING");
        if (!string.IsNullOrWhiteSpace(full))
            return EnsureDatabase(full, database: "contoso_it");

        // GitHub Actions uses a base connection string without Database=.
        var baseConn = Environment.GetEnvironmentVariable("CONTOSO_IT_SQL_BASE");
        if (!string.IsNullOrWhiteSpace(baseConn))
            return EnsureDatabase(baseConn, database: "contoso_it");

        // tests/OrderAccept.IntegrationTests/appsettings.json
        var fromConfig = configuration.GetConnectionString("Contoso");
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return EnsureDatabase(fromConfig, database: "contoso_it");

        // Local fallback: build from SA_PASSWORD or docker-compose default.
        var saPassword =
            Environment.GetEnvironmentVariable("SA_PASSWORD")
            ?? "Your_strong_Password123!";

        return $"Server=localhost,1433;Database=contoso_it;User ID=sa;Password={saPassword};TrustServerCertificate=True;Encrypt=False";
    }

    private static string EnsureDatabase(string connectionString, string database)
    {
        // Append/replace Database= / Initial Catalog= without adding new dependencies.
        var parts = connectionString.Trim().TrimEnd(';')
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.StartsWith("Database=", StringComparison.OrdinalIgnoreCase)
                     && !p.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase));

        return string.Join(';', parts.Append($"Database={database}")) + ";";
    }

    private static string? TryFindRepoRoot(string startPath)
    {
        try
        {
            var dir = new DirectoryInfo(startPath);
            for (var i = 0; i < 12 && dir is not null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "docker-compose.yml"))
                    && Directory.Exists(Path.Combine(dir.FullName, "infra")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }
        catch
        {
            // Best-effort only.
        }

        return null;
    }

    private static IEnumerable<(string Key, string? Value)> ReadDotEnvFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.StartsWith('#'))
                continue;

            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();

            // Strip optional surrounding quotes.
            if (value.Length >= 2 && ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
                value = value[1..^1];

            yield return (key, value);
        }
    }
}
