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
                .WithName("Orders")
                .WithOpenApi();

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
            CancellationToken cancellationToken)
        {
            // Generate correlation ID at the API entry point
            var correlationId = correlationIdProvider.GenerateCorrelationId();

            // Create command with correlation ID
            var command = new AcceptOrderCommand(request, correlationId);

            // Execute handler
            await handler.HandleAsync(command, cancellationToken);

            // Return response with correlation ID (para tracing del cliente)
            return Results.Accepted(
                $"/api/orders/{correlationId}",
                new { correlationId = correlationId.Value });
        }
    }
}