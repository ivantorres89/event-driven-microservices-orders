using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Query;
using OrderAccept.Persistence.Impl.Repositories.Base;

namespace OrderAccept.Persistence.Impl.Repositories.Query;

public sealed class ProductQueryRepository : BaseEfQueryRepository<Product>, IProductQueryRepository
{
    public ProductQueryRepository(ContosoDbContext db, ILogger<ProductQueryRepository> logger)
        : base(db, logger)
    {
    }

    public Task<Product?> GetByExternalIdAsync(
        string externalProductId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(p => p.ExternalProductId == externalProductId, asNoTracking, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetAllAsync(
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        // We intentionally keep read queries lean and deterministic.
        // Ordering by Id gives stable pagination.
        return await Queryable(asNoTracking)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetPagedAsync(
        int offset,
        int size,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0) offset = 0;
        if (size <= 0) size = 50;

        return await Queryable(asNoTracking)
            .OrderBy(p => p.Id)
            .Skip(offset)
            .Take(size)
            .ToListAsync(cancellationToken);
    }
}
