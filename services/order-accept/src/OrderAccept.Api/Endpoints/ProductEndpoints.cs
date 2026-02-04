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
            group.MapGet("/", GetProducts)
                .WithName("GetProducts")
                .WithSummary("Get all products (optionally paged)")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);

            // GET /api/products/{id}
            group.MapGet("/{id:long}", GetProductById)
                .WithName("GetProductById")
                .WithSummary("Get a product by its internal id")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status401Unauthorized);
        }

        private static async Task<IResult> GetProducts(
            int? offset,
            int? size,
            IGetProductsHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.HandleAsync(offset, size, cancellationToken);
            return Results.Ok(result);
        }

        private static async Task<IResult> GetProductById(
            long id,
            IGetProductByIdHandler handler,
            CancellationToken cancellationToken)
        {
            var product = await handler.HandleAsync(id, cancellationToken);
            return product is null ? Results.NotFound() : Results.Ok(product);
        }
    }
}
