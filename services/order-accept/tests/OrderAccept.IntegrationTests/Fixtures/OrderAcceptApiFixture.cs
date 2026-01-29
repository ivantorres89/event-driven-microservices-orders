using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OrderAccept.Api;

namespace OrderAccept.IntegrationTests.Fixtures;

public sealed class OrderAcceptApiFixture : IAsyncLifetime
{   
    private readonly ITestcontainersContainer _redis;
    private readonly ITestcontainersContainer _rabbit;
    private readonly bool _useLocalInfra;
    private bool _useLocalInfraResolved;
    
    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public string RedisConnectionString => _useLocalInfraResolved ? "localhost:6379" : $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";
    public string RabbitConnectionString => _useLocalInfraResolved ? "amqp://guest:guest@localhost:5672/" : $"amqp://guest:guest@{_rabbit.Hostname}:{_rabbit.GetMappedPublicPort(5672)}/";
    public string RabbitQueueName => "order.accepted.it";

    public OrderAcceptApiFixture()
    {
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");

        _useLocalInfra = string.Equals(Environment.GetEnvironmentVariable("USE_LOCAL_INFRA"), "true", StringComparison.OrdinalIgnoreCase);

        _redis = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();

        _rabbit = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("rabbitmq:3.13-alpine")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
            .Build();
    }

    public async Task InitializeAsync()
    {
        _useLocalInfraResolved = _useLocalInfra || IsLocalInfraAvailable();

        if (!_useLocalInfraResolved)
        {
            await _redis.StartAsync();
            await _rabbit.StartAsync();
        }

        Factory = new CustomWebApplicationFactory(this);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            Factory.Dispose();

        if (!_useLocalInfraResolved)
        {
            await _rabbit.DisposeAsync();
            await _redis.DisposeAsync();
        }
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
