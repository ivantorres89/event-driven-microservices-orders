using OpenTelemetry;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Requests;
using System.Diagnostics;

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
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status503ServiceUnavailable);
        }

        private static async Task<IResult> AcceptOrder(
            CreateOrderRequest request,
            IAcceptOrderHandler handler,
            ICorrelationIdProvider correlationIdProvider,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            var correlationId = correlationIdProvider.GetCorrelationId();

            // HTTP Request generated Correlation Id
            // Attach correlation id to the current OpenTelemetry context
            var correlationValue = correlationId.Value.ToString();
            Activity.Current?.SetTag("correlation_id", correlationValue);
            Baggage.SetBaggage("correlation_id", correlationValue);
            var command = new AcceptOrderCommand(request);

            await handler.HandleAsync(command, cancellationToken);

            // Expose correlation id in response header for tracing
            http.Response.Headers["X-Correlation-Id"] = correlationValue;

            // Return 202 + JSON body
            return Results.Json(
                new { correlationId = correlationId.Value },
                statusCode: StatusCodes.Status202Accepted
            );
        }
    }
}