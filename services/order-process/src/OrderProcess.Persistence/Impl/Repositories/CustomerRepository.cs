using Microsoft.EntityFrameworkCore;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal sealed class CustomerRepository : EfRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(ContosoDbContext db) : base(db) { }

    public Task<Customer?> GetByExternalIdAsync(string externalCustomerId, CancellationToken cancellationToken = default)
        => Db.Customers.SingleOrDefaultAsync(x => x.ExternalCustomerId == externalCustomerId, cancellationToken);
}
