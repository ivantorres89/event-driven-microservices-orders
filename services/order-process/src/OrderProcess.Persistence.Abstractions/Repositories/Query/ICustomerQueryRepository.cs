using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Base;

namespace OrderProcess.Persistence.Abstractions.Repositories.Query;

public interface ICustomerQueryRepository : IQueryRepository<Customer>
{
    Task<Customer?> GetByExternalIdAsync(string externalCustomerId, CancellationToken cancellationToken = default);
}
