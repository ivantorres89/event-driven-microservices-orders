using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IProductRepository : IBaseRepository<Product>
{
    Task<Product?> GetByExternalIdAsync(string externalProductId, CancellationToken cancellationToken = default);
}
