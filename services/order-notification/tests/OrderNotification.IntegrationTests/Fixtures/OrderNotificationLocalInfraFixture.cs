using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace OrderNotification.IntegrationTests.Fixtures;

/// <summary>
/// Integration test fixture that matches the style used in other services:
/// - Assumes local infra is started externally (docker compose up -d)
/// - Fails fast if RabbitMQ/Redis ports are not open
///
/// In CI, GitHub Actions starts the same docker compose infra before running these tests.
/// </summary>
public sealed class OrderNotificationLocalInfraFixture : IAsyncLifetime
{
    private readonly IConfiguration _configuration;
    private bool _infraResolved;

    public OrderNotificationLocalInfraFixture()
    {
        _configuration = BuildConfiguration();
    }

    public IConfiguration Configuration => _configuration;

    public string RedisConnectionString =>
        _configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException(
            "Missing ConnectionStrings:Redis. Provide it via tests/OrderNotification.IntegrationTests/appsettings.json or environment variables.");

    public string RabbitConnectionString =>
        _configuration.GetConnectionString("RabbitMq")
        ?? throw new InvalidOperationException(
            "Missing ConnectionStrings:RabbitMq. Provide it via tests/OrderNotification.IntegrationTests/appsettings.json or environment variables.");

    // Use a dedicated queue for integration tests to avoid clashing with dev runs.
    public string RabbitInboundQueueName =>
        _configuration["Messaging:InboundQueueName"] ?? "order.processed.it";

    public string RabbitOutboundQueueName =>
        _configuration["Messaging:OutboundQueueName"] ?? "order.processed.it";

    public TimeSpan WorkflowTtl =>
        _configuration.GetValue<TimeSpan?>("WorkflowState:Ttl") ?? TimeSpan.FromMinutes(30);

    public IConnectionMultiplexer Redis { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _infraResolved = IsLocalInfraAvailable();

        if (!_infraResolved)
        {
            throw new InvalidOperationException(
                "Local infra not detected. Run 'docker compose up -d' (RabbitMQ + Redis) before running integration tests.");
        }

        // Make it easy for any component that uses the default configuration conventions
        // (ConnectionStrings__Redis, etc.) to resolve the IT settings.
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", RedisConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", RabbitConnectionString);

        Redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (!_infraResolved)
            return;

        if (Redis is not null)
        {
            await Redis.CloseAsync();
            Redis.Dispose();
        }
    }

    public IDatabase GetRedisDb() => Redis.GetDatabase();

    private bool IsLocalInfraAvailable()
        => IsRedisAvailable(RedisConnectionString)
           && IsRabbitAvailable(RabbitConnectionString);

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
}
