using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderAccept.Api.Endpoints;
using OrderAccept.Application;
using OrderAccept.Infrastructure;
using Serilog;
using Serilog.Exceptions;

namespace OrderAccept.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
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
        builder.Services.AddAuthorization();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddHealthChecks();

        builder.Services
            .AddOrderAcceptApplication()
            .AddOrderAcceptInfrastructure(builder.Configuration);

        // --- OpenTelemetry (Tracing + Metrics) ---
        var otelEnabled = builder.Configuration.GetValue<bool?>("OpenTelemetry:Enabled") ?? true;

        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "order-accept";
        var serviceVersion = builder.Configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        if (otelEnabled)
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(
                    serviceName: serviceName, serviceVersion: serviceVersion))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }).WithMetrics(metrics =>

                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                });
        }            

        var app = builder.Build();

        // --- HTTP pipeline ---
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseSerilogRequestLogging();

        app.UseHttpsRedirection();
        app.UseAuthorization();

        // Health checks (simple for orchestration)
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        // Endpoints
        app.MapOrderAcceptEndpoints();

        app.Run();
    }
}
