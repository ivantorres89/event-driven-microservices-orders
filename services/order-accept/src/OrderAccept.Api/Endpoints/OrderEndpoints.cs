using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Requests;

namespace OrderAccept.Api.Endpoints
{
    /// <summary>
    /// Minimal API endpoints for order operations.
    /// </summary>
    public static class OrderEndpoints
    {
        public static void MapOrderAcceptEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/orders")
                .WithName("Orders");

            group.MapPost("/accept", AcceptOrder)
                .WithName("AcceptOrder")
                .WithSummary("Accept an order")
                .Produces(StatusCodes.Status202Accepted)
                .Produces(StatusCodes.Status400BadRequest);
        }

        private static async Task<IResult> AcceptOrder(
            CreateOrderRequest request,
            IAcceptOrderHandler handler,
            ICorrelationIdProvider correlationIdProvider,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            var correlationId = correlationIdProvider.GetCorrelationId();
            var command = new AcceptOrderCommand(request);

            await handler.HandleAsync(command, cancellationToken);

            // Expose correlation id in response header for tracing
            http.Response.Headers["X-Correlation-ID"] = correlationId.Value.ToString();

            // Return just correlationId on body with HTTP status 202 (Accepted) for tracing SPA purposes.
            http.Response.StatusCode = StatusCodes.Status202Accepted;
            await http.Response.WriteAsJsonAsync(new { correlationId = correlationId.Value }, cancellationToken);

            return Results.StatusCode(StatusCodes.Status202Accepted);
        }
    }
}