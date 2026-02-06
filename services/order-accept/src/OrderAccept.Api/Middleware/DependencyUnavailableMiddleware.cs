using OrderAccept.Shared.Resilience;
using System.Diagnostics;

namespace OrderAccept.Api.Middleware;

/// <summary>
/// Maps critical dependency outages (Redis/RabbitMQ) to HTTP 500 ProblemDetails,
/// as required by the API contract (no 503 in the public API).
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
            _logger.LogError(ex, "Dependency unavailable");

            if (context.Response.HasStarted)
                throw;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var instance = $"{context.Request.Path}{context.Request.QueryString}";
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            var payload = new
            {
                type = "https://httpstatuses.com/500",
                title = "Internal Server Error",
                status = 500,
                detail = ex.Message,
                instance,
                traceId
            };

            await context.Response.WriteAsJsonAsync(
                payload,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: default);
        }
    }
}

public static class DependencyUnavailableMiddlewareExtensions
{
    public static IApplicationBuilder UseDependencyUnavailableHandling(this IApplicationBuilder app)
        => app.UseMiddleware<DependencyUnavailableMiddleware>();
}
