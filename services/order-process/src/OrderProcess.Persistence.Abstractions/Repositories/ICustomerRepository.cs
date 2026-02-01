using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByExternalIdAsync(string externalCustomerId, CancellationToken cancellationToken = default);
    void Add(Customer customer);
}
