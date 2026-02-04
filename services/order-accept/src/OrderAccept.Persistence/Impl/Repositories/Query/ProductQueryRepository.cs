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

    protected override IQueryable<Product> Queryable(bool asNoTracking)
    {
        // Contract rules:
        // - Products: IsActive = 1 and IsSoftDeleted = 0
        var q = Db.Products.Where(p => !p.IsSoftDeleted && p.IsActive);
        return asNoTracking ? q.AsNoTracking() : q;
    }

    public Task<Product?> GetByExternalIdAsync(
        string externalProductId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(p => p.ExternalProductId == externalProductId, asNoTracking, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByExternalIdsAsync(
        IReadOnlyCollection<string> externalProductIds,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        if (externalProductIds is null || externalProductIds.Count == 0)
            return Array.Empty<Product>();

        // Note: using Contains(...) with a materialized list translates to IN (...) in SQL.
        return await Queryable(asNoTracking)
            .Where(p => externalProductIds.Contains(p.ExternalProductId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        // Ordering by Id gives stable pagination / deterministic output.
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
        return await Queryable(asNoTracking)
            .OrderBy(p => p.Id)
            .Skip(offset)
            .Take(size)
            .ToListAsync(cancellationToken);
    }
}
