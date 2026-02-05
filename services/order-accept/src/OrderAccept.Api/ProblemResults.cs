using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace OrderAccept.Api;

/// <summary>
/// Helper methods to return RFC 7807 Problem Details.
///
/// The API contract expects:
/// - Content-Type: application/problem+json
/// - camelCase JSON
/// - traceId at the root (not nested)
/// </summary>
internal static class ProblemResults
{
    private const string ContentType = "application/problem+json";

    public static IResult BadRequest(HttpContext http, string detail)
        => Problem(http, StatusCodes.Status400BadRequest, "Bad Request", detail);

    public static IResult Unauthorized(HttpContext http, string detail = "Unauthorized")
        => Problem(http, StatusCodes.Status401Unauthorized, "Unauthorized", detail);

    public static IResult Forbidden(HttpContext http, string detail = "Forbidden")
        => Problem(http, StatusCodes.Status403Forbidden, "Forbidden", detail);

    public static IResult NotFound(HttpContext http, string detail = "Not Found")
        => Problem(http, StatusCodes.Status404NotFound, "Not Found", detail);

    public static IResult InternalError(HttpContext http, string detail = "An unexpected error occurred.")
        => Problem(http, StatusCodes.Status500InternalServerError, "Internal Server Error", detail);

    public static IResult ValidationProblem(HttpContext http, IDictionary<string, string[]> errors)
    {
        var status = StatusCodes.Status400BadRequest;
        var instance = BuildInstance(http);
        var traceId = BuildTraceId(http);

        // ValidationProblemDetails-like payload (minimal + contract-friendly)
        var payload = new
        {
            type = $"https://httpstatuses.com/{status}",
            title = "One or more validation errors occurred.",
            status,
            errors,
            instance,
            traceId
        };

        return Results.Json(payload, ResolveSerializerOptions(http), statusCode: status, contentType: ContentType);
    }

    public static IResult Problem(HttpContext http, int status, string title, string detail)
    {
        var instance = BuildInstance(http);
        var traceId = BuildTraceId(http);

        var payload = new
        {
            type = $"https://httpstatuses.com/{status}",
            title,
            status,
            detail,
            instance,
            traceId
        };

        return Results.Json(payload, ResolveSerializerOptions(http), statusCode: status, contentType: ContentType);
    }

    private static string BuildInstance(HttpContext http)
        => $"{http.Request.Path}{http.Request.QueryString}";

    private static string BuildTraceId(HttpContext http)
        => Activity.Current?.Id ?? http.TraceIdentifier;

    private static JsonSerializerOptions ResolveSerializerOptions(HttpContext http)
    {
        var options = http.RequestServices?.GetService<Microsoft.Extensions.Options.IOptions<JsonOptions>>();
        return options?.Value?.SerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }
}
