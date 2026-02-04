using FluentValidation;
using OpenTelemetry;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Requests;
using System.Diagnostics;
using System.Security.Claims;

namespace OrderAccept.Api.Endpoints
{
    /// <summary>
    /// Minimal API endpoints for order operations.
    /// </summary>
    public static class OrderEndpoints
    {
        public static void MapOrderEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/orders")
                .WithName("Orders")
                .RequireAuthorization();

            // POST /api/orders
            group.MapPost("/", AcceptOrder)
                .WithName("CreateOrder")
                .WithSummary("Accept an order")
                .Produces(StatusCodes.Status202Accepted)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status503ServiceUnavailable);

            // GET /api/orders?offset=&size=
            group.MapGet("/", GetOrders)
                .WithName("GetOrders")
                .WithSummary("Get orders for the current user (JWT sub => Customer.ExternalCustomerId)")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);

            // DELETE /api/orders/{id}
            group.MapDelete("/{id:long}", SoftDeleteOrder)
                .WithName("SoftDeleteOrder")
                .WithSummary("Soft delete an order (only if it belongs to the current user)")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status401Unauthorized);
        }

        private static async Task<IResult> AcceptOrder(
            CreateOrderRequest request,
            IValidator<CreateOrderRequest> validator,
            IAcceptOrderHandler handler,
            ICorrelationIdProvider correlationIdProvider,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            // Authenticated user identity (JWT subject). This is the only trusted source of CustomerId.
            var subject = GetSubject(http.User);
            if (string.IsNullOrWhiteSpace(subject))
                return Results.Unauthorized();

            // If the client sends CustomerId, enforce it matches the JWT to avoid spoofing.
            if (!string.IsNullOrWhiteSpace(request.CustomerId) && !string.Equals(request.CustomerId, subject, StringComparison.Ordinal))
                return Results.Forbid();

            // Force CustomerId to the JWT subject (do not trust request body).
            var effectiveRequest = request with { CustomerId = subject };

            var validation = await validator.ValidateAsync(effectiveRequest, cancellationToken);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(ToValidationDictionary(validation));
            }

            var correlationId = correlationIdProvider.GetCorrelationId();

            // Attach correlation id to the current OpenTelemetry context
            var correlationValue = correlationId.Value.ToString();
            Activity.Current?.SetTag("correlation_id", correlationValue);
            Baggage.SetBaggage("correlation_id", correlationValue);

            var command = new AcceptOrderCommand(effectiveRequest);
            await handler.HandleAsync(command, cancellationToken);

            // Expose correlation id in response header for tracing
            http.Response.Headers["X-Correlation-Id"] = correlationValue;

            // Return 202 + JSON body
            return Results.Json(
                new { correlationId = correlationId.Value },
                statusCode: StatusCodes.Status202Accepted
            );
        }

        private static async Task<IResult> GetOrders(
            int? offset,
            int? size,
            IGetOrdersHandler handler,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            var subject = GetSubject(http.User);
            if (string.IsNullOrWhiteSpace(subject))
                return Results.Unauthorized();

            var safeOffset = Math.Max(0, offset ?? 0);
            var safeSize = Math.Max(1, size ?? 50);

            var result = await handler.HandleAsync(subject, safeOffset, safeSize, cancellationToken);
            return Results.Ok(result);
        }

        private static async Task<IResult> SoftDeleteOrder(
            long id,
            ISoftDeleteOrderHandler handler,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            var subject = GetSubject(http.User);
            if (string.IsNullOrWhiteSpace(subject))
                return Results.Unauthorized();

            var deleted = await handler.HandleAsync(id, subject, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }

        private static string? GetSubject(ClaimsPrincipal user)
            => user.FindFirstValue("sub")
               ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("oid");

        private static Dictionary<string, string[]> ToValidationDictionary(FluentValidation.Results.ValidationResult result)
            => result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
    }
}
