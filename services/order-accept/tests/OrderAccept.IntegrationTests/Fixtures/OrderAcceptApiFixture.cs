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
    
    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public string RedisConnectionString => "localhost:6379";
    public string RabbitConnectionString => "amqp://guest:guest@localhost:5672/";
    public string RabbitQueueName => "order.accepted.it";

    // Dedicated DB for integration tests (keeps local dev DB clean).
    public string ContosoConnectionString =>
        "Server=localhost,1433;Database=contoso_it;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";

    // Symmetric key used by the test host for JwtBearer validation (dev/demo mode).
    public string JwtSigningKey => "dev-it-signing-key-please-change-123456";



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
}
