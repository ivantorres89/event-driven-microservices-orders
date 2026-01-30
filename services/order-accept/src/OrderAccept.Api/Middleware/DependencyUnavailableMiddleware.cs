using System.Net.Mime;
using System.Text.Json;
using OrderAccept.Shared.Resilience;

namespace OrderAccept.Api.Middleware;

/// <summary>
/// Maps critical dependency outages (Redis/RabbitMQ) to HTTP 503.
/// </summary>
public sealed class DependencyUnavailableMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DependencyUnavailableMiddleware> _logger;

    public DependencyUnavailableMiddleware(RequestDelegate next, ILogger<DependencyUnavailableMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DependencyUnavailableException ex)
        {
            _logger.LogWarning(ex, "Dependency unavailable: returning 503");

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = MediaTypeNames.Application.Json;

                // Preserve correlation id header if already set by the endpoint.
                if (!context.Response.Headers.ContainsKey("X-Correlation-Id") &&
                    context.Request.Headers.TryGetValue("X-Correlation-Id", out var corr))
                {
                    context.Response.Headers["X-Correlation-Id"] = corr.ToString();
                }

                var payload = new
                {
                    error = "service_unavailable",
                    message = ex.Message
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
                return;
            }

            throw;
        }
    }
}

public static class DependencyUnavailableMiddlewareExtensions
{
    public static IApplicationBuilder UseDependencyUnavailableHandling(this IApplicationBuilder app)
        => app.UseMiddleware<DependencyUnavailableMiddleware>();
}
