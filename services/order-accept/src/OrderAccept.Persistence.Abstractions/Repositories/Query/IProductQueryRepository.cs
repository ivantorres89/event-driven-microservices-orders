using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Base;

namespace OrderAccept.Persistence.Abstractions.Repositories.Query;

public interface IProductQueryRepository : IQueryRepository<Product>
{
    Task<Product?> GetByExternalIdAsync(
        string externalProductId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByExternalIdsAsync(
        IReadOnlyCollection<string> externalProductIds,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetAllAsync(
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetPagedAsync(
        int offset,
        int size,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);
}
