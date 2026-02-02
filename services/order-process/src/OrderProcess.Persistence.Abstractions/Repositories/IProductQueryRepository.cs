using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Base;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IProductQueryRepository : IQueryRepository<Product>
{
    Task<Product?> GetByExternalIdAsync(string externalProductId, CancellationToken cancellationToken = default);
}
