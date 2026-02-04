using Microsoft.Extensions.Logging;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Query;
using OrderAccept.Persistence.Impl.Repositories.Base;

namespace OrderAccept.Persistence.Impl.Repositories.Query;

public sealed class CustomerQueryRepository : BaseEfQueryRepository<Customer>, ICustomerQueryRepository
{
    public CustomerQueryRepository(ContosoDbContext db, ILogger<CustomerQueryRepository> logger)
        : base(db, logger)
    {
    }

    public Task<Customer?> GetByExternalIdAsync(
        string externalCustomerId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(c => c.ExternalCustomerId == externalCustomerId, asNoTracking, cancellationToken);
}
