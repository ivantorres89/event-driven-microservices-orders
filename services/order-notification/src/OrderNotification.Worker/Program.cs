using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderNotification.Application;
using OrderNotification.Infrastructure;
using OrderNotification.Worker.Auth;
using OrderNotification.Worker.Hubs;
using OrderNotification.Worker.Observability;
using Serilog;
using Serilog.Exceptions;

namespace OrderNotification.Worker;

/// <summary>
/// Background worker + SignalR hub host.
///
/// We keep the same service wiring, logging and OpenTelemetry configuration as order-process.
/// This service additionally exposes a SignalR hub for real-time order status notifications.
/// </summary>
public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Background worker (kept as-is)
        builder.Services.AddHostedService<Worker>();

        // CORS for local SPA
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("spa-dev", policy =>
            {
                policy
                    .WithOrigins("http://localhost:4200", "https://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        // JWT Auth (DEV symmetric key)
        var signingKey = builder.Configuration["DevJwt:SigningKey"] ?? string.Empty;
        if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("DevJwt:SigningKey is required in Development.");
        }

        var issuer = builder.Configuration["DevJwt:Issuer"];
        var audience = builder.Configuration["DevJwt:Audience"];

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),

                    // In DEV you can keep these relaxed by leaving Issuer/Audience empty in config.
                    ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // IMPORTANT for SignalR in browser:
                // WebSockets cannot set Authorization header; SignalR sends JWT as query param: ?access_token=...
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

        // --- Logging (Serilog) ---
        builder.Services.AddSerilog((services, cfg) =>
        {
            cfg.ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithExceptionDetails()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId();
        });

        // --- Services ---
        builder.Services
            .AddOrderNotificationApplication()
            .AddOrderNotificationInfrastructure(builder.Configuration, builder.Environment);

        // --- Authorization ---
        builder.Services.AddAuthorization();
        // Real-time notifier implementation
        builder.Services.AddSingleton<OrderNotification.Application.Abstractions.IOrderStatusNotifier, OrderNotification.Worker.Notifiers.SignalROrderStatusNotifier>();


        // User mapping for SignalR: use the authenticated user identifier.
        builder.Services.AddSingleton<IUserIdProvider, ClaimsUserIdProvider>();

        // --- SignalR (Redis backplane) ---
        var redisConn = builder.Configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConn))
            throw new InvalidOperationException("ConnectionStrings:Redis is required");

        var channelPrefix = builder.Configuration["SignalR:ChannelPrefix"] ?? "contoso-signalr";

        builder.Services
            .AddSignalR()
            .AddStackExchangeRedis(redisConn, o =>
            {
                o.Configuration.ChannelPrefix = channelPrefix;
            });

        // --- OpenTelemetry (Tracing + Metrics) ---
        var otelEnabled = builder.Configuration.GetValue<bool?>("OpenTelemetry:Enabled") ?? true;

        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "order-notification";
        var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        if (otelEnabled)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddHttpClientInstrumentation()
                        .AddProcessor(new CorrelationIdActivityProcessor())
                        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation()
                        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                });
        }

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseCors("spa-dev");
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // --- Health endpoints (K8s probes + graceful drain) ---
        // During scale-in, Kubernetes will start terminating the pod. We "drain" in two steps:
        //  1) preStop touches /tmp/draining so readiness flips to 503 immediately (no new traffic).
        //  2) SIGTERM triggers ApplicationStopping; we keep returning 503 and let existing connections finish/reconnect.
        const string drainFilePath = "/tmp/draining";
        volatile bool isDraining = false;
        app.Lifetime.ApplicationStopping.Register(() => isDraining = true);

        static bool IsReady(bool draining, string drainFile) => !(draining || File.Exists(drainFile));

        app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
            .AllowAnonymous();

        app.MapGet("/health/ready", () =>
                IsReady(isDraining, drainFilePath)
                    ? Results.Ok(new { status = "ready" })
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous();

        // Backwards-compatible endpoint (kept for convenience)
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
            .AllowAnonymous();

        // DEV: Issue a JWT for a given userId (used by SPA for REST + SignalR).
        // POST /dev/token  { "userId": "contoso-user-001" }
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/dev/token", (DevTokenRequest request, IConfiguration config) =>
            {
                var userId = (request?.UserId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Results.BadRequest(new { error = "userId is required" });
                }

                var key = config["DevJwt:SigningKey"] ?? string.Empty;
                var iss = config["DevJwt:Issuer"];
                var aud = config["DevJwt:Audience"];

                var minutes = 120;
                if (int.TryParse(config["DevJwt:ExpiresMinutes"], out var m) && m > 0)
                {
                    minutes = m;
                }

                var now = DateTimeOffset.UtcNow;
                var expires = now.AddMinutes(minutes);

                // IMPORTANT:
                // - NameIdentifier ensures SignalR UserIdentifier is set by default.
                // - sub is commonly used too.
                var claims = new List<Claim>
                {
                    new(JwtRegisteredClaimNames.Sub, userId),
                    new(ClaimTypes.NameIdentifier, userId),
                    new(ClaimTypes.Name, userId),
                };

                var signingKeyBytes = Encoding.UTF8.GetBytes(key);
                var securityKey = new SymmetricSecurityKey(signingKeyBytes);
                var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: string.IsNullOrWhiteSpace(iss) ? null : iss,
                    audience: string.IsNullOrWhiteSpace(aud) ? null : aud,
                    claims: claims,
                    notBefore: now.UtcDateTime,
                    expires: expires.UtcDateTime,
                    signingCredentials: creds);

                var jwt = new JwtSecurityTokenHandler().WriteToken(token);

                return Results.Ok(new DevTokenResponse
                {
                    UserId = userId,
                    Token = jwt,
                    ExpiresAtUtc = expires.UtcDateTime
                });
            }).AllowAnonymous()
              .RequireCors("spa-dev");
        }

        // SignalR hub endpoint (standard naming conventions)
        var hub = app.MapHub<OrderStatusHub>("/hubs/order-status")
            .RequireAuthorization();

        if (app.Environment.IsDevelopment())
        {
            hub.RequireCors("spa-dev");
        }

        app.Run();
    }

    public sealed record DevTokenRequest(string UserId);

    public sealed record DevTokenResponse
    {
        public required string UserId { get; init; }
        public required string Token { get; init; }
        public required DateTime ExpiresAtUtc { get; init; }
    }
}
