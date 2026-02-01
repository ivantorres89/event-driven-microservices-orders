using Microsoft.EntityFrameworkCore;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly ContosoDbContext _db;

    public CustomerRepository(ContosoDbContext db) => _db = db;

    public Task<Customer?> GetByExternalIdAsync(string externalCustomerId, CancellationToken cancellationToken = default)
        => _db.Customers.SingleOrDefaultAsync(x => x.ExternalCustomerId == externalCustomerId, cancellationToken);

    public void Add(Customer customer) => _db.Customers.Add(customer);
}
