using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Query;
using OrderProcess.Persistence.Impl.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Query;

public sealed class ProductQueryRepository : BaseEfQueryRepository<Product>, IProductQueryRepository
{
    public ProductQueryRepository(ContosoDbContext db, ILogger<ProductQueryRepository> logger)
        : base(db, logger)
    {
    }

    public Task<Product?> GetByExternalIdAsync(string externalProductId, CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(p => p.ExternalProductId == externalProductId, cancellationToken);
}
