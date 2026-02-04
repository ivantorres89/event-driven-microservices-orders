using OrderAccept.Application.Abstractions;

namespace OrderAccept.Api.Endpoints
{
    /// <summary>
    /// Minimal API endpoints for product catalog.
    /// </summary>
    public static class ProductEndpoints
    {
        public static void MapProductEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/products")
                .WithName("Products")
                .RequireAuthorization();

            // GET /api/products?offset=&size=
            group.MapGet("", GetProducts)
                .WithName("GetProducts")
                .WithSummary("Get a paged list of active products")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status500InternalServerError);

            // GET /api/products/{id}
            // Where {id} is the product ExternalProductId (string)
            group.MapGet("/{id}", GetProductById)
                .WithName("GetProductById")
                .WithSummary("Get a product by its externalProductId")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status500InternalServerError);
        }

        private static async Task<IResult> GetProducts(
            int? offset,
            int? size,
            IGetProductsHandler handler,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            var effectiveOffset = offset ?? 0;
            var effectiveSize = size ?? 12;

            if (effectiveOffset < 0 || effectiveSize < 1 || effectiveSize > 100)
                return ProblemResults.BadRequest(http, "Invalid pagination parameters.");

            var result = await handler.HandleAsync(effectiveOffset, effectiveSize, cancellationToken);
            return Results.Ok(result);
        }

        private static async Task<IResult> GetProductById(
            string id,
            IGetProductByIdHandler handler,
            HttpContext http,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
                return ProblemResults.BadRequest(http, "Missing or invalid product id.");

            var product = await handler.HandleAsync(id, cancellationToken);
            return product is null
                ? ProblemResults.NotFound(http, "Product not found.")
                : Results.Ok(product);
        }
    }
}
