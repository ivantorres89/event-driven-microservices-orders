using FluentValidation;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Requests;
using OrderAccept.Application.Handlers;
using System.Security.Claims;
using System.Text.Json;

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

            // GET /api/orders?offset=&size=
            group.MapGet("", GetOrders)
                .WithName("GetOrders")
                .WithSummary("List orders (paged, by authenticated user)")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status500InternalServerError);

            // GET /api/orders/{id}
            group.MapGet("/{id:long}", GetOrderById)
                .WithName("GetOrderById")
                .WithSummary("Get order detail by id (authenticated user)")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status500InternalServerError);

            // POST /api/orders
            group.MapPost("", AcceptOrder)
                .WithName("CreateOrder")
                .WithSummary("Create an order")
                .Produces(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status409Conflict)
                .Produces(StatusCodes.Status500InternalServerError);

            // DELETE /api/orders/{id}
            group.MapDelete("/{id:long}", SoftDeleteOrder)
                .WithName("SoftDeleteOrder")
                .WithSummary("Soft-delete an order (ownership validated by authenticated user)")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status500InternalServerError);
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
            {
                // Token is valid (endpoint requires auth), but identity claim is missing.
                return ProblemResults.Forbidden(http, "Missing required identity claim (sub/nameidentifier).");
            }

            var effectiveOffset = offset ?? 0;
            var effectiveSize = size ?? 10;

            if (effectiveOffset < 0 || effectiveSize < 1 || effectiveSize > 100)
                return ProblemResults.BadRequest(http, "Invalid pagination parameters.");

            var result = await handler.HandleAsync(subject, effectiveOffset, effectiveSize, cancellationToken);
            return Results.Ok(result);
        }

        private static async Task<IResult> GetOrderById(
            long id,
            IGetOrderByIdHandler handler,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            if (id < 1)
                return ProblemResults.BadRequest(http, "Invalid order id.");

            var subject = GetSubject(http.User);
            if (string.IsNullOrWhiteSpace(subject))
            {
                // Token is valid (endpoint requires auth), but identity claim is missing.
                return ProblemResults.Forbidden(http, "Missing required identity claim (sub/nameidentifier).");
            }

            var order = await handler.HandleAsync(subject, id, cancellationToken);
            return order is null
                ? ProblemResults.NotFound(http, "Order not found.")
                : Results.Ok(order);
        }

        private static async Task<IResult> AcceptOrder(
            CreateOrderRequest request,
            IValidator<CreateOrderRequest> validator,
            IAcceptOrderHandler handler,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            var subject = GetSubject(http.User);
            if (string.IsNullOrWhiteSpace(subject))
            {
                // Token is valid (endpoint requires auth), but identity claim is missing.
                return ProblemResults.Forbidden(http, "Missing required identity claim (sub/nameidentifier).");
            }

            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return ProblemResults.ValidationProblem(http, ToValidationDictionary(validation));
            }

            try
            {
                var dto = await handler.HandleAsync(new AcceptOrderCommand(subject, request), cancellationToken);

                // Location header is required by the contract.
                var location = $"/api/orders/{dto.Id}";
                return Results.Created(location, dto);
            }
            catch (ProductNotFoundException ex)
            {
                return ProblemResults.NotFound(http, ex.Message);
            }
        }

        private static async Task<IResult> SoftDeleteOrder(
            long id,
            ISoftDeleteOrderHandler handler,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            if (id < 1)
                return ProblemResults.BadRequest(http, "Invalid order id.");

            var subject = GetSubject(http.User);
            if (string.IsNullOrWhiteSpace(subject))
            {
                // Token is valid (endpoint requires auth), but identity claim is missing.
                return ProblemResults.Forbidden(http, "Missing required identity claim (sub/nameidentifier).");
            }

            var outcome = await handler.HandleAsync(id, subject, cancellationToken);

            return outcome switch
            {
                SoftDeleteOrderOutcome.Deleted => Results.NoContent(),
                SoftDeleteOrderOutcome.Forbidden => ProblemResults.Forbidden(http, "Order does not belong to the authenticated user."),
                _ => ProblemResults.NotFound(http, "Order not found.")
            };
        }

        private static string? GetSubject(ClaimsPrincipal user)
            => user.FindFirstValue("sub")
               ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("oid");

        private static Dictionary<string, string[]> ToValidationDictionary(FluentValidation.Results.ValidationResult result)
            => result.Errors
                .GroupBy(e => ToCamelCasePath(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        private static string ToCamelCasePath(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return propertyName;

            var segments = propertyName.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var bracketIndex = segment.IndexOf('[');
                var namePart = bracketIndex < 0 ? segment : segment[..bracketIndex];
                var indexPart = bracketIndex < 0 ? string.Empty : segment[bracketIndex..];

                if (!string.IsNullOrWhiteSpace(namePart))
                {
                    namePart = JsonNamingPolicy.CamelCase.ConvertName(namePart);
                }

                segments[i] = namePart + indexPart;
            }

            return string.Join('.', segments);
        }
    }
}
