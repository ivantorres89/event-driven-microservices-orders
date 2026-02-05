using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OrderAccept.Api;

namespace OrderAccept.UnitTests.API;

public sealed class ProblemResultsTests
{
    [Fact]
    public async Task BadRequest_WritesProblemDetails()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-1";
        context.Request.Path = "/api/orders";
        context.Response.Body = new MemoryStream();
        context.RequestServices = BuildServices();

        var result = ProblemResults.BadRequest(context, "bad input");
        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);

        doc.RootElement.GetProperty("detail").GetString().Should().Be("bad input");
        doc.RootElement.GetProperty("instance").GetString().Should().Be("/api/orders");
        doc.RootElement.GetProperty("traceId").GetString().Should().Be("trace-1");
    }

    [Fact]
    public async Task ValidationProblem_WritesValidationPayload()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-2";
        context.Request.Path = "/api/orders";
        context.Response.Body = new MemoryStream();
        context.RequestServices = BuildServices();

        var errors = new Dictionary<string, string[]>
        {
            ["Items[0].ProductId"] = new[] { "ProductId is required." }
        };

        var result = ProblemResults.ValidationProblem(context, errors);
        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);

        doc.RootElement.GetProperty("errors").GetProperty("Items[0].ProductId").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("traceId").GetString().Should().Be("trace-2");
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<JsonOptions>(_ => { });
        return services.BuildServiceProvider();
    }
}