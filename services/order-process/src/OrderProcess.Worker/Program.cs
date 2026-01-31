using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderProcess.Application;
using OrderProcess.Infrastructure;
using Serilog;
using Serilog.Exceptions;

namespace OrderProcess.Worker;

public partial class Program
{
    public static void Main(string[] args)
    {
        // We host a minimal ASP.NET Core pipeline only for health endpoints (K8s readiness/liveness).
        var builder = WebApplication.CreateBuilder(args);

        // --- Logging (Serilog) ---
        builder.Host.UseSerilog((ctx, services, cfg) =>
        {
            cfg.ReadFrom.Configuration(ctx.Configuration)
               .Enrich.FromLogContext()
               .Enrich.WithExceptionDetails()
               .Enrich.WithEnvironmentName()
               .Enrich.WithProcessId()
               .Enrich.WithThreadId();
        });

        // --- Services ---
        builder.Services.AddHealthChecks();

        builder.Services
            .AddOrderProcessApplication()
            .AddOrderProcessInfrastructure(builder.Configuration, builder.Environment);

        // --- OpenTelemetry (Tracing + Metrics) ---
        var otelEnabled = builder.Configuration.GetValue<bool?>("OpenTelemetry:Enabled") ?? true;

        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "order-process";
        var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        if (otelEnabled)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddProcessor(new Observability.CorrelationIdActivityProcessor())
                        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                });
        }

        var app = builder.Build();

        app.UseSerilogRequestLogging();

        // Health checks (simple for orchestration)
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        app.Run();
    }
}
