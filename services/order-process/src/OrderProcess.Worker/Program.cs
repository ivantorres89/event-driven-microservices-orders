using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderProcess.Application;
using OrderProcess.Infrastructure;
using Serilog;
using Serilog.Exceptions;
using Microsoft.Extensions.Hosting;


namespace OrderProcess.Worker;

/// <summary>
/// Pure background worker host (no HTTP pipeline / no ASP.NET Core).
///
/// Kubernetes liveness/readiness are handled via K8s strategy (not via in-process HTTP health endpoints).
/// </summary>
public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

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
                        .AddHttpClientInstrumentation()
                        .AddProcessor(new Observability.CorrelationIdActivityProcessor())
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

        var host = builder.Build();
        host.Run();
    }
}
