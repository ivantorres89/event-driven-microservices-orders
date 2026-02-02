using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByExternalIdAsync(string externalCustomerId, CancellationToken cancellationToken = default);
}
