using Microsoft.EntityFrameworkCore;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal sealed class ProductRepository : EfRepository<Product>, IProductRepository
{
    public ProductRepository(ContosoDbContext db) : base(db) { }

    public Task<Product?> GetByExternalIdAsync(string externalProductId, CancellationToken cancellationToken = default)
        => Db.Products.SingleOrDefaultAsync(x => x.ExternalProductId == externalProductId, cancellationToken);
}
