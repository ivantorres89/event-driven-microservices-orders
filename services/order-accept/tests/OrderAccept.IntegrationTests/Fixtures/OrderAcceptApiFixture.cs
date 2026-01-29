using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OrderAccept.Api;

namespace OrderAccept.IntegrationTests.Fixtures;

public sealed class OrderAcceptApiFixture : IAsyncLifetime
{   
    private bool _useLocalInfraResolved;
    
    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public string RedisConnectionString => "localhost:6379";
    public string RabbitConnectionString => "amqp://guest:guest@localhost:5672/";
    public string RabbitQueueName => "order.accepted.it";

    public OrderAcceptApiFixture()
    {
    }

    public async Task InitializeAsync()
    {
        _useLocalInfraResolved = IsLocalInfraAvailable();

        if (!_useLocalInfraResolved)
        {
            throw new InvalidOperationException("Local infra not detected. Run 'docker compose up -d' in infra/local before running integration tests.");
        }

        Factory = new CustomWebApplicationFactory(this);
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
        return IsPortOpen("localhost", 6379) && IsPortOpen("localhost", 5672);
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
                    ["RabbitMQ:ConnectionString"] = _fixture.RabbitConnectionString,
                    ["RabbitMQ:QueueName"] = _fixture.RabbitQueueName,
                    ["OpenTelemetry:Enabled"] = "false"
                };

                config.AddInMemoryCollection(settings);
            });
        }
    }
}
