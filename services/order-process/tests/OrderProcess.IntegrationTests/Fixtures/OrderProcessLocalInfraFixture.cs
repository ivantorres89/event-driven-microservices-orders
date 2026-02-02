using System.Net.Sockets;
using FluentMigrator.Runner;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private bool _useLocalInfraResolved;

    public string RedisConnectionString => "localhost:6379";
    public string RabbitConnectionString => "amqp://guest:guest@localhost:5672/";

    /// <summary>
    /// Dedicated SQL database used ONLY for integration tests.
    /// </summary>
    public string SqlDatabaseName => "contoso-integrationtests";

    /// <summary>
    /// Base SQL Server connection string (without DB). Override via env var CONTOSO_IT_SQL_BASE.
    /// Example: "Server=localhost,1433;User ID=sa;Password=...;TrustServerCertificate=True;Encrypt=False".
    /// </summary>
    private static string SqlBaseConnectionString =>
        Environment.GetEnvironmentVariable("CONTOSO_IT_SQL_BASE")
        ?? "Server=localhost,1433;User ID=sa;Password=Your_strong_Password123!;TrustServerCertificate=True;Encrypt=False";

    public string SqlConnectionString
    {
        get
        {
            var b = new SqlConnectionStringBuilder(SqlBaseConnectionString)
            {
                InitialCatalog = SqlDatabaseName
            };
            return b.ConnectionString;
        }
    }

    // Use dedicated queues for integration tests to avoid clashing with dev runs.
    public string RabbitInboundQueueName => "order.accepted.it";
    public string RabbitOutboundQueueName => "order.processed.it";

    public async Task InitializeAsync()
    {
        _useLocalInfraResolved = IsLocalInfraAvailable();

        if (!_useLocalInfraResolved)
        {
            throw new InvalidOperationException(
                "Local infra not detected. Run 'docker compose up -d' (RabbitMQ + Redis + SQL Server) before running integration tests.");
        }

        // Ensure every integration test run uses the dedicated DB (never the Development DB)
        // so tests can run in parallel with local development without clobbering data.
        Environment.SetEnvironmentVariable("ConnectionStrings__Contoso", SqlConnectionString);

        // Avoid race conditions when xUnit instantiates the fixture per test class.
        // We initialize the SQL database once per test process, and keep it stable for the full run.
        await InitLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            await RecreateDatabaseAsync(SqlBaseConnectionString, SqlDatabaseName);
            ApplyMigrations(SqlConnectionString);

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private bool IsLocalInfraAvailable()
        => IsPortOpen("localhost", 6379)
           && IsPortOpen("localhost", 5672)
           && IsSqlServerReachable(SqlBaseConnectionString);

    private static bool IsSqlServerReachable(string baseConn)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(baseConn) { InitialCatalog = "master" };
            using var conn = new SqlConnection(b.ConnectionString);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RecreateDatabaseAsync(string baseConn, string dbName)
    {
        var master = new SqlConnectionStringBuilder(baseConn) { InitialCatalog = "master" };
        await using var conn = new SqlConnection(master.ConnectionString);
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

    private static void ApplyMigrations(string connectionString)
    {
        // Run migrations from OrderProcess.DatabaseMigrations. This avoids duplicated SQL scripts in tests
        // and validates the migrations project itself.
        var services = new ServiceCollection();

        services.AddLogging(lb => lb
            .AddConsole()
            .SetMinimumLevel(LogLevel.Warning));

        services
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSqlServer()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(ContosoVersionTable).Assembly).For.Migrations()
                .WithVersionTable(new ContosoVersionTable()))
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        using var sp = services.BuildServiceProvider(validateScopes: false);
        using var scope = sp.CreateScope();

        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
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
}
