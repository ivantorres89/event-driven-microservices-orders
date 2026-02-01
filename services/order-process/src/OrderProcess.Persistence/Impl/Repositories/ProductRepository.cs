using Microsoft.EntityFrameworkCore;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly ContosoDbContext _db;

    public ProductRepository(ContosoDbContext db) => _db = db;

    public Task<Product?> GetByExternalIdAsync(string externalProductId, CancellationToken cancellationToken = default)
        => _db.Products.SingleOrDefaultAsync(x => x.ExternalProductId == externalProductId, cancellationToken);

    public void Add(Product product) => _db.Products.Add(product);
}
