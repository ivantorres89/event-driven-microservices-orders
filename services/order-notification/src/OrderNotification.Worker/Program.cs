using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
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

        // --- Authentication / Authorization ---
        // Development uses a lightweight authentication handler that treats the Bearer token value as the user id.
        if (builder.Environment.IsDevelopment())
        {
            builder.Services
                .AddAuthentication(DevAuthenticationDefaults.Scheme)
                .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
                    DevAuthenticationDefaults.Scheme,
                    _ => { });
        }
        else
        {
            // Placeholder for production auth (typically JWT bearer). Configure as needed.
            builder.Services.AddAuthentication();
        }

        builder.Services.AddAuthorization();
        // Real-time notifier implementation
        builder.Services.AddSingleton<OrderNotification.Application.Abstractions.IOrderStatusNotifier, OrderNotification.Worker.Notifiers.SignalROrderStatusNotifier>();


        // User mapping for SignalR: use the authenticated user identifier.
        builder.Services.AddSingleton<IUserIdProvider, ClaimsUserIdProvider>();

        // --- SignalR (Redis backplane) ---
        var redisConn = builder.Configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConn))
            throw new InvalidOperationException("ConnectionStrings:Redis is required");

        builder.Services
            .AddSignalR()
            .AddStackExchangeRedis(redisConn);

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

        app.UseAuthentication();
        app.UseAuthorization();

        // SignalR hub endpoint (standard naming conventions)
        app.MapHub<OrderStatusHub>("/hubs/order-status");

        app.Run();
    }
}
