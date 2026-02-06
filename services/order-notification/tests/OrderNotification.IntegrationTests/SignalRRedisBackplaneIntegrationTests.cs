using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderNotification.Application.Abstractions;
using OrderNotification.Infrastructure.Correlation;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.IntegrationTests.Fixtures;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;
using OrderNotification.Worker.Auth;
using OrderNotification.Worker.Hubs;
using OrderNotification.Worker.Hubs.Models;
using OrderNotification.Worker.Notifiers;
using StackExchange.Redis;

namespace OrderNotification.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class SignalRRedisBackplaneIntegrationTests
{
    private readonly OrderNotificationLocalInfraFixture _fixture;

    public SignalRRedisBackplaneIntegrationTests(OrderNotificationLocalInfraFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task ClientsUser_FansOutAcrossTwoServers_ViaRedisBackplane()
    {
        var redisConn = _fixture.RedisConnectionString;
        var channelPrefix = $"contoso-signalr-it-{Guid.NewGuid():N}";
        var signingKey = "dev-it-signing-key-please-change-123456";
        var userId = $"user-it-{Guid.NewGuid():N}";

        await using var serverA = await TestServerHost.StartAsync(redisConn, channelPrefix, signingKey);
        await using var serverB = await TestServerHost.StartAsync(redisConn, channelPrefix, signingKey);

        var token = JwtToken(signingKey, userId);

        var receivedA = new TaskCompletionSource<OrderStatusNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedB = new TaskCompletionSource<OrderStatusNotification>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var clientA = serverA.CreateClientConnection(token, receivedA);
        await using var clientB = serverB.CreateClientConnection(token, receivedB);

        await clientA.StartAsync();
        await clientB.StartAsync();

        var correlationId = CorrelationId.New();
        await serverA.Notifier.NotifyStatusChangedAsync(userId, correlationId, OrderWorkflowStatus.Completed, 777);

        var msgA = await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var msgB = await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(15));

        msgA.CorrelationId.Should().Be(correlationId);
        msgA.Status.Should().Be(OrderWorkflowStatus.Completed);
        msgA.OrderId.Should().Be(777);

        msgB.Should().Be(msgA, "Redis backplane should fan-out to connections on other pods");
    }

    [Fact]
    public async Task Hub_RegisterOrder_StoresCorrelationMappingInRedis()
    {
        var redisConn = _fixture.RedisConnectionString;
        var channelPrefix = $"contoso-signalr-it-{Guid.NewGuid():N}";
        var signingKey = "dev-it-signing-key-please-change-123456";
        var userId = $"user-it-{Guid.NewGuid():N}";

        await using var server = await TestServerHost.StartAsync(redisConn, channelPrefix, signingKey);

        var token = JwtToken(signingKey, userId);
        var received = new TaskCompletionSource<OrderStatusNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client = server.CreateClientConnection(token, received);
        await client.StartAsync();

        var correlationId = CorrelationId.New();

        await client.InvokeAsync("RegisterOrder", correlationId.ToString());

        var db = server.Redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderUserMap(correlationId);
        var raw = await db.StringGetAsync(key);
        raw.ToString().Should().Be(userId);
    }

    private static string JwtToken(string signingKey, string userId)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(30);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
        };

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: null,
            audience: null,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class TestServerHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly string _baseAddress;

        private TestServerHost(WebApplication app, string baseAddress)
        {
            _app = app;
            _baseAddress = baseAddress.TrimEnd('/');
        }

        public required IOrderStatusNotifier Notifier { get; init; }
        public required IConnectionMultiplexer Redis { get; init; }

        public static async Task<TestServerHost> StartAsync(string redisConn, string channelPrefix, string signingKey)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            // Bind to a dynamic local port.
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            builder.Services.AddLogging();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            if (!string.IsNullOrEmpty(accessToken)
                                && path.StartsWithSegments("/hubs/order-status"))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<IUserIdProvider, ClaimsUserIdProvider>();

            // Redis connections (one for workflow keys, one for SignalR backplane).
            var mux = await ConnectionMultiplexer.ConnectAsync(redisConn);
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ => mux);

            builder.Services.AddSingleton<IOrderCorrelationRegistry>(sp =>
                new RedisOrderCorrelationRegistry(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    Options.Create(new WorkflowStateOptions { Ttl = TimeSpan.FromMinutes(30) }),
                    NullLogger<RedisOrderCorrelationRegistry>.Instance));

            builder.Services.AddSingleton<IOrderWorkflowStateQuery>(sp =>
                new RedisOrderWorkflowStateQuery(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    Options.Create(new WorkflowStateOptions { Ttl = TimeSpan.FromMinutes(30) }),
                    NullLogger<RedisOrderWorkflowStateQuery>.Instance));

            builder.Services
                .AddSignalR(options =>
                {
                    options.EnableDetailedErrors = true;
                })
                .AddStackExchangeRedis(redisConn, o =>
                {
                    o.Configuration.ChannelPrefix = channelPrefix;
                });

            builder.Services.AddSingleton<IOrderStatusNotifier, SignalROrderStatusNotifier>();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapHub<OrderStatusHub>("/hubs/order-status").RequireAuthorization();

            await app.StartAsync();

            var addresses = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
            var baseAddress = addresses?.FirstOrDefault() ?? throw new InvalidOperationException("Server did not expose an address.");

            return new TestServerHost(app, baseAddress)
            {
                Notifier = app.Services.GetRequiredService<IOrderStatusNotifier>(),
                Redis = mux
            };
        }

        public Microsoft.AspNetCore.SignalR.Client.HubConnection CreateClientConnection(
            string jwt,
            TaskCompletionSource<OrderStatusNotification> received)
        {
            var url = $"{_baseAddress}/hubs/order-status";

            var conn = new Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(jwt)!;
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                    options.SkipNegotiation = false;
                })
                .WithAutomaticReconnect()
                .Build();

            conn.On<OrderStatusNotification>("Notification", msg =>
            {
                received.TrySetResult(msg);
            });

            return conn;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();

            try
            {
                await Redis.CloseAsync();
            }
            finally
            {
                Redis.Dispose();
            }
        }
    }
}
