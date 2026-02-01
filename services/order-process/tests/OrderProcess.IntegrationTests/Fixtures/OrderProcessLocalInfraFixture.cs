using System.Net.Sockets;

namespace OrderProcess.IntegrationTests.Fixtures;

/// <summary>
/// Matches the style used in OrderAccept integration tests:
/// - Assumes local infra is started externally (docker compose up -d)
/// - Fails fast if RabbitMQ/Redis ports are not open.
/// </summary>
public sealed class OrderProcessLocalInfraFixture : IAsyncLifetime
{
    private bool _useLocalInfraResolved;

    public string RedisConnectionString => "localhost:6379";
    public string RabbitConnectionString => "amqp://guest:guest@localhost:5672/";

    // Use dedicated queues for integration tests to avoid clashing with dev runs.
    public string RabbitInboundQueueName => "order.accepted.it";
    public string RabbitOutboundQueueName => "order.processed.it";

    public Task InitializeAsync()
    {
        _useLocalInfraResolved = IsLocalInfraAvailable();

        if (!_useLocalInfraResolved)
        {
            throw new InvalidOperationException(
                "Local infra not detected. Run 'docker compose up -d' (RabbitMQ + Redis) before running integration tests.");
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static bool IsLocalInfraAvailable()
        => IsPortOpen("localhost", 6379) && IsPortOpen("localhost", 5672);

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
