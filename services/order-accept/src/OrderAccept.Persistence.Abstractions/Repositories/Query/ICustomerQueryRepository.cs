using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Base;

namespace OrderAccept.Persistence.Abstractions.Repositories.Query;

public interface ICustomerQueryRepository : IQueryRepository<Customer>
{
    Task<Customer?> GetByExternalIdAsync(
        string externalCustomerId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);
}
