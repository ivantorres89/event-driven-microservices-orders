using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderAccept.Api;
using OrderProcess.DatabaseMigrations;

namespace OrderAccept.IntegrationTests.Fixtures;

/// <summary>
/// Integration test fixture that:
/// - Assumes local infra is started externally (docker compose up -d)
/// - Resets + migrates the shared SQL database ONCE per test run (local dev)
/// - Can skip DB reset/migrate in CI (CONTOSO_IT_SKIP_DB_RESET=true), when migrations are run centrally in the pipeline
/// </summary>
public sealed class OrderAcceptApiFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _initialized;

    private readonly IConfiguration _configuration;
    private bool _infraResolved;

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public OrderAcceptApiFixture()
    {
        _configuration = BuildConfiguration();
    }

    public IConfiguration Configuration => _configuration;

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

    /// <summary>
    /// Dedicated SQL database connection string used ONLY for integration tests.
    /// Resolved in this order:
    /// 1) CONTOSO_IT_SQL_CONNECTIONSTRING (full, must include Database=)
    /// 2) CONTOSO_IT_SQL_BASE (base without Database=, used in GitHub Actions)
    /// 3) ConnectionStrings:Contoso (from tests appsettings.json)
    /// 4) SA_PASSWORD (env or infra/local/.env)
    /// 5) docker-compose default: Your_strong_Password123!
    ///
    /// Database name is forced to: contoso-integrationtests
    /// </summary>
    public string ContosoConnectionString => ResolveContosoConnectionString(_configuration);

    public string SqlDatabaseName
    {
        get
        {
            var b = new SqlConnectionStringBuilder(ContosoConnectionString);
            if (string.IsNullOrWhiteSpace(b.InitialCatalog))
                throw new InvalidOperationException("ConnectionStrings:Contoso must include a database name (Initial Catalog / Database).");
            return b.InitialCatalog;
        }
    }

    // Symmetric key used by the test host for JwtBearer validation (dev/demo mode).
    public string JwtSigningKey => "dev-it-signing-key-please-change-123456";

    public async Task InitializeAsync()
    {
        _infraResolved = IsLocalInfraAvailable();

        if (!_infraResolved)
        {
            throw new InvalidOperationException(
                "Local infra not detected. Run 'docker compose up -d' (RabbitMQ + Redis + SQL Server) before running integration tests.");
        }

        // Make it easy for any component that uses the default configuration conventions
        // (ConnectionStrings__Contoso, etc.) to resolve the IT settings.
        Environment.SetEnvironmentVariable("ConnectionStrings__Contoso", ContosoConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", RedisConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", RabbitConnectionString);

        var skipReset = IsTrue(Environment.GetEnvironmentVariable("CONTOSO_IT_SKIP_DB_RESET"));

        // Avoid race conditions if the test runner instantiates fixtures more than once.
        await InitLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            EnsureSafeDatabaseName();

            if (!skipReset)
            {
                var sqlBase = BuildMasterConnectionString(ContosoConnectionString);
                await RecreateDatabaseAsync(sqlBase, SqlDatabaseName);
                ContosoMigrator.MigrateUp(ContosoConnectionString);
            }

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }

        Factory = new CustomWebApplicationFactory(this);

        // Warm up host.
        _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();

        if (!_infraResolved)
            return;

        var skipReset = IsTrue(Environment.GetEnvironmentVariable("CONTOSO_IT_SKIP_DB_RESET"));
        if (skipReset)
            return;

        var dropDb = _configuration.GetValue("IntegrationTests:Sql:DropDatabaseOnDispose", defaultValue: true);
        if (!dropDb)
            return;

        await InitLock.WaitAsync();
        try
        {
            if (!_initialized)
                return;

            EnsureSafeDatabaseName();

            var sqlBase = BuildMasterConnectionString(ContosoConnectionString);
            await DropDatabaseIfExistsAsync(sqlBase, SqlDatabaseName);

            _initialized = false;
        }
        finally
        {
            InitLock.Release();
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
                    ["Jwt:Authority"] = string.Empty,
                    ["Jwt:Audience"] = string.Empty,
                    ["OpenTelemetry:Enabled"] = "false"
                };

                config.AddInMemoryCollection(settings);
            });
        }
    }

    private bool IsLocalInfraAvailable()
        => IsRedisAvailable(RedisConnectionString)
           && IsRabbitAvailable(RabbitConnectionString)
           && IsSqlServerReachable(BuildMasterConnectionString(ContosoConnectionString));

    private static bool IsSqlServerReachable(string masterConn)
    {
        try
        {
            using var conn = new SqlConnection(masterConn);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RecreateDatabaseAsync(string masterConn, string dbName)
    {
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();

        var safeDbNameForLiteral = dbName.Replace("'", "''");
        var safeDbNameForBracket = dbName.Replace("]", "]]");

        var cmdText = $@"
IF DB_ID(N'{safeDbNameForLiteral}') IS NOT NULL
BEGIN
    ALTER DATABASE [{safeDbNameForBracket}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{safeDbNameForBracket}];
END
CREATE DATABASE [{safeDbNameForBracket}];";

        await using var cmd = new SqlCommand(cmdText, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseIfExistsAsync(string masterConn, string dbName)
    {
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();

        var safeDbNameForLiteral = dbName.Replace("'", "''");
        var safeDbNameForBracket = dbName.Replace("]", "]]");

        var cmdText = $@"
IF DB_ID(N'{safeDbNameForLiteral}') IS NOT NULL
BEGIN
    ALTER DATABASE [{safeDbNameForBracket}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{safeDbNameForBracket}];
END";

        await using var cmd = new SqlCommand(cmdText, conn);
        await cmd.ExecuteNonQueryAsync();
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
        const string dbName = "contoso-integrationtests";

        // Highest precedence: full connection string override.
        var full = Environment.GetEnvironmentVariable("CONTOSO_IT_SQL_CONNECTIONSTRING");
        if (!string.IsNullOrWhiteSpace(full))
            return EnsureDatabase(full, database: dbName);

        // GitHub Actions uses a base connection string without Database=.
        var baseConn = Environment.GetEnvironmentVariable("CONTOSO_IT_SQL_BASE");
        if (!string.IsNullOrWhiteSpace(baseConn))
            return EnsureDatabase(baseConn, database: dbName);

        // tests/OrderAccept.IntegrationTests/appsettings.json
        var fromConfig = configuration.GetConnectionString("Contoso");
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return EnsureDatabase(fromConfig, database: dbName);

        // Local fallback: build from SA_PASSWORD or docker-compose default.
        var saPassword =
            Environment.GetEnvironmentVariable("SA_PASSWORD")
            ?? "Your_strong_Password123!";

        return $"Server=localhost,1433;Database={dbName};User ID=sa;Password={saPassword};TrustServerCertificate=True;Encrypt=False";
    }

    private static string EnsureDatabase(string connectionString, string database)
    {
        // Append/replace Database= / Initial Catalog=.
        var parts = connectionString.Trim().TrimEnd(';')
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.StartsWith("Database=", StringComparison.OrdinalIgnoreCase)
                     && !p.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase));

        return string.Join(';', parts.Append($"Database={database}")) + ";";
    }

    private static string BuildMasterConnectionString(string contosoConnectionString)
    {
        var b = new SqlConnectionStringBuilder(contosoConnectionString)
        {
            InitialCatalog = "master"
        };
        return b.ConnectionString;
    }

    private void EnsureSafeDatabaseName()
    {
        var expectedFragment = _configuration["IntegrationTests:Sql:RequireDatabaseNameContains"];
        if (string.IsNullOrWhiteSpace(expectedFragment))
            expectedFragment = "integration";

        if (SqlDatabaseName.IndexOf(expectedFragment, StringComparison.OrdinalIgnoreCase) < 0)
        {
            throw new InvalidOperationException(
                $"Refusing to reset database '{SqlDatabaseName}'. " +
                $"For safety, IntegrationTests:Sql:RequireDatabaseNameContains must match the DB name (currently '{expectedFragment}').");
        }
    }

    private static bool IsRedisAvailable(string redisConn)
    {
        // For most redis connection strings, host:port is the first segment.
        var hostPort = redisConn.Split(',')[0].Trim();
        if (hostPort.Contains('='))
        {
            // Handle 'host=...,...' styles by falling back to localhost defaults.
            return IsPortOpen("localhost", 6379);
        }

        var parts = hostPort.Split(':');
        var host = parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 6379;
        return IsPortOpen(host, port);
    }

    private static bool IsRabbitAvailable(string rabbitConn)
    {
        try
        {
            var uri = new Uri(rabbitConn);
            var port = uri.Port > 0 ? uri.Port : 5672;
            return IsPortOpen(uri.Host, port);
        }
        catch
        {
            // If not a URI, best effort fallback.
            return IsPortOpen("localhost", 5672);
        }
    }

    private static bool IsPortOpen(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            return connectTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTrue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
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
