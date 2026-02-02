using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderProcess.DatabaseMigrations;

namespace OrderProcess.IntegrationTests.Fixtures;

/// <summary>
/// Matches the style used in OrderAccept integration tests:
/// - Assumes local infra is started externally (docker compose up -d)
/// - Fails fast if RabbitMQ/Redis ports are not open.
/// </summary>
public sealed class OrderProcessLocalInfraFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _initialized;

    private readonly IConfiguration _configuration;
    private bool _infraResolved;

    public OrderProcessLocalInfraFixture()
    {
        _configuration = BuildConfiguration();
    }

    public IConfiguration Configuration => _configuration;

    public string RedisConnectionString =>
        _configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis. Provide it via tests/OrderProcess.IntegrationTests/appsettings.json or environment variables.");

    public string RabbitConnectionString =>
        _configuration.GetConnectionString("RabbitMq")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:RabbitMq. Provide it via tests/OrderProcess.IntegrationTests/appsettings.json or environment variables.");

    /// <summary>
    /// Dedicated SQL database connection string used ONLY for integration tests.
    /// </summary>
    public string SqlConnectionString =>
        _configuration.GetConnectionString("Contoso")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Contoso. Provide it via tests/OrderProcess.IntegrationTests/appsettings.json or environment variables.");

    public string SqlDatabaseName
    {
        get
        {
            var b = new SqlConnectionStringBuilder(SqlConnectionString);
            if (string.IsNullOrWhiteSpace(b.InitialCatalog))
                throw new InvalidOperationException("ConnectionStrings:Contoso must include a database name (Initial Catalog / Database).");
            return b.InitialCatalog;
        }
    }

    // Use dedicated queues for integration tests to avoid clashing with dev runs.
    public string RabbitInboundQueueName =>
        _configuration["Messaging:InboundQueueName"] ?? "order.accepted.it";

    public string RabbitOutboundQueueName =>
        _configuration["Messaging:OutboundQueueName"] ?? "order.processed.it";

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
        Environment.SetEnvironmentVariable("ConnectionStrings__Contoso", SqlConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", RedisConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", RabbitConnectionString);

        // Avoid race conditions if the test runner instantiates fixtures more than once.
        await InitLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            EnsureSafeDatabaseName();

            var sqlBase = BuildMasterConnectionString(SqlConnectionString);
            await RecreateDatabaseAsync(sqlBase, SqlDatabaseName);
            ContosoMigrator.MigrateUp(SqlConnectionString);

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    public async Task DisposeAsync()
    {
        if (!_infraResolved)
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

            var sqlBase = BuildMasterConnectionString(SqlConnectionString);
            await DropDatabaseIfExistsAsync(sqlBase, SqlDatabaseName);

            _initialized = false;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private bool IsLocalInfraAvailable()
        => IsRedisAvailable(RedisConnectionString)
           && IsRabbitAvailable(RabbitConnectionString)
           && IsSqlServerReachable(BuildMasterConnectionString(SqlConnectionString));

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

        var cmdText = $@"
IF DB_ID(N'{dbName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{dbName}];
END
CREATE DATABASE [{dbName}];";

        await using var cmd = new SqlCommand(cmdText, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseIfExistsAsync(string masterConn, string dbName)
    {
        await using var conn = new SqlConnection(masterConn);
        await conn.OpenAsync();

        var cmdText = $@"
IF DB_ID(N'{dbName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{dbName}];
END";

        await using var cmd = new SqlCommand(cmdText, conn);
        await cmd.ExecuteNonQueryAsync();
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

    private static IConfiguration BuildConfiguration()
    {
        var env =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
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
}
