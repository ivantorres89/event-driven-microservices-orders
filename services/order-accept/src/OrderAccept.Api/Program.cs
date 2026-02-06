using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderAccept.Api.Endpoints;
using OrderAccept.Api.Middleware;
using OrderAccept.Application;
using OrderAccept.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Exceptions;
using System.Text;

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

        // --- CORS (SPA) ---
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("SpaCors", policy =>
                policy.WithOrigins("http://localhost:4200")
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });

        // --- Security (JWT) ---
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claims as-is (avoid inbound claim mapping surprises)
                options.MapInboundClaims = false;

                var authority = builder.Configuration["Jwt:Authority"];
                var audience = builder.Configuration["Jwt:Audience"];
                var signingKey = builder.Configuration["Jwt:SigningKey"];

                if (!string.IsNullOrWhiteSpace(authority))
                {
                    // Production-like: validate using OIDC metadata from an IDP
                    options.Authority = authority;
                    if (!string.IsNullOrWhiteSpace(audience))
                        options.Audience = audience;

                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                }
                else
                {
                    // Development/demo: symmetric key validation
                    if (string.IsNullOrWhiteSpace(signingKey))
                        throw new InvalidOperationException("Either Jwt:Authority or Jwt:SigningKey must be configured");

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                        ClockSkew = TimeSpan.FromMinutes(2)
                    };
                }
            });

        builder.Services.AddAuthorization();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "OrderAccept API",
                Version = "v1"
            });

            // JWT Bearer auth
            var scheme = new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
            };

            c.AddSecurityDefinition("Bearer", scheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { scheme, Array.Empty<string>() }
            });

});

        // FluentValidation (minimal API: validate explicitly in endpoints)
        builder.Services.AddValidatorsFromAssemblyContaining<OrderAccept.Api.Validators.CreateOrderRequestValidator>();

        builder.Services.AddHealthChecks();

        builder.Services
            .AddOrderAcceptApplication()
            .AddOrderAcceptInfrastructure(builder.Configuration, builder.Environment);

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
                        .AddProcessor(new Observability.CorrelationIdActivityProcessor())
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

// Standardize unhandled exceptions as RFC 7807 problem details.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;

        logger.LogError(ex, "Unhandled exception");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var instance = $"{context.Request.Path}{context.Request.QueryString}";
        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

        var payload = new
        {
            type = "https://httpstatuses.com/500",
            title = "Internal Server Error",
            status = 500,
            detail = ex?.Message ?? "An unexpected error occurred.",
            instance,
            traceId
        };

        await context.Response.WriteAsJsonAsync(payload);
    });
});

// Convert critical dependency outages (Redis/RabbitMQ) into problem+json responses (500 by contract).
app.UseDependencyUnavailableHandling();

app.UseHttpsRedirection();

app.UseCors("SpaCors");

app.UseStatusCodePages(async statusCodeContext =>
{
    var http = statusCodeContext.HttpContext;

    if (http.Response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        await ProblemResults.Unauthorized(http).ExecuteAsync(http);
    }
    else if (http.Response.StatusCode == StatusCodes.Status403Forbidden)
    {
        await ProblemResults.Forbidden(http).ExecuteAsync(http);
    }
});

app.UseAuthentication();
app.UseAuthorization();


// Health checks (simple for orchestration)
app.MapHealthChecks("/health/live").RequireAuthorization();
app.MapHealthChecks("/health/ready").RequireAuthorization();

// Endpoints
app.MapProductEndpoints();
app.MapOrderEndpoints();

app.Run();
    }
}
