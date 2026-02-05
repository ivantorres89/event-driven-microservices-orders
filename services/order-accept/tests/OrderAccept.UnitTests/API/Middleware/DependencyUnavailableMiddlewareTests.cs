using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Api.Middleware;
using OrderAccept.Shared.Resilience;

namespace OrderAccept.UnitTests.API.Middleware;

public sealed class DependencyUnavailableMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenDependencyUnavailable_ReturnsProblemJson()
    {
        var logger = Mock.Of<ILogger<DependencyUnavailableMiddleware>>();
        RequestDelegate next = _ => throw new DependencyUnavailableException("redis down");

        var middleware = new DependencyUnavailableMiddleware(next, logger);

        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-1";
        context.Request.Path = "/api/orders/1";
        context.Request.QueryString = new QueryString("?a=1");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);

        doc.RootElement.GetProperty("detail").GetString().Should().Be("redis down");
        doc.RootElement.GetProperty("instance").GetString().Should().Be("/api/orders/1?a=1");
        doc.RootElement.GetProperty("traceId").GetString().Should().Be("trace-1");
    }

    [Fact]
    public async Task InvokeAsync_WhenResponseHasStarted_Rethrows()
    {
        var logger = Mock.Of<ILogger<DependencyUnavailableMiddleware>>();
        RequestDelegate next = _ => throw new DependencyUnavailableException("redis down");

        var middleware = new DependencyUnavailableMiddleware(next, logger);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature
        {
            Body = context.Response.Body,
            Headers = new HeaderDictionary(),
            StatusCode = StatusCodes.Status200OK
        });

        var act = async () => await middleware.InvokeAsync(context);

        await act.Should().ThrowAsync<DependencyUnavailableException>();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_CallsNext()
    {
        var logger = Mock.Of<ILogger<DependencyUnavailableMiddleware>>();
        var nextCalled = false;

        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        };

        var middleware = new DependencyUnavailableMiddleware(next, logger);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; set; } = true;
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public string? ReasonPhrase { get; set; }
        public int StatusCode { get; set; }

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}