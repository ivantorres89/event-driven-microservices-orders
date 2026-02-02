using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Query;
using OrderProcess.Persistence.Impl.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Query;

public sealed class CustomerQueryRepository : BaseEfQueryRepository<Customer>, ICustomerQueryRepository
{
    public CustomerQueryRepository(ContosoDbContext db, ILogger<CustomerQueryRepository> logger)
        : base(db, logger)
    {
    }

    public Task<Customer?> GetByExternalIdAsync(string externalCustomerId, CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(c => c.ExternalCustomerId == externalCustomerId, cancellationToken);
}
