using System.Net.Sockets;
using Microsoft.Data.SqlClient;

namespace OrderProcess.IntegrationTests.Fixtures;

/// <summary>
/// Local infra fixture:
/// - Assumes local infra is started externally (docker compose up -d)
/// - Fails fast if Redis/RabbitMQ/SQL ports are not open.
/// - Ensures the Contoso database exists and schema is applied (using the same SQL scripts as the migrations project).
/// </summary>
public sealed class OrderProcessLocalInfraFixture : IAsyncLifetime
{
    private bool _useLocalInfraResolved;

    public string RedisConnectionString => "localhost:6379";
    public string RabbitConnectionString => "amqp://guest:guest@localhost:5672/";

    // Matches the README defaults in the local docker compose stack.
    public string SqlConnectionString => "Server=localhost,1433;Database=contoso;User ID=sa;Password=Your_strong_Password123!;TrustServerCertificate=True;Encrypt=False";

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

        await EnsureDatabaseReadyAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static bool IsLocalInfraAvailable()
        => IsPortOpen("localhost", 6379) && IsPortOpen("localhost", 5672) && IsPortOpen("localhost", 1433);

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

    private async Task EnsureDatabaseReadyAsync()
    {
        // 1) Ensure database exists
        var builder = new SqlConnectionStringBuilder(SqlConnectionString);
        var dbName = builder.InitialCatalog;

        var masterBuilder = new SqlConnectionStringBuilder(SqlConnectionString)
        {
            InitialCatalog = "master"
        };

        await using (var master = new SqlConnection(masterBuilder.ConnectionString))
        {
            await master.OpenAsync();

            var cmdText = @$"IF DB_ID(N'{dbName}') IS NULL
BEGIN
    CREATE DATABASE [{dbName}];
END";

            await using var cmd = new SqlCommand(cmdText, master);
            await cmd.ExecuteNonQueryAsync();
        }

        // 2) Apply schema script (idempotent)
        var scriptsDir = Path.Combine(AppContext.BaseDirectory, "Sql");
        var initSchemaPath = Path.Combine(scriptsDir, "0001_InitSchema.sql");

        if (!File.Exists(initSchemaPath))
            throw new InvalidOperationException($"Migration SQL not found at '{initSchemaPath}'. Ensure the integration test project copies migration scripts to output.");

        var ddl = await File.ReadAllTextAsync(initSchemaPath);

        await using (var conn = new SqlConnection(SqlConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(ddl, conn)
            {
                CommandTimeout = 60
            };
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
