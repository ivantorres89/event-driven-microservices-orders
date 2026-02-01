using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByExternalIdAsync(string externalProductId, CancellationToken cancellationToken = default);
    void Add(Product product);
}
