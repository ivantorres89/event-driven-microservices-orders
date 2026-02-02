using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

public sealed class CustomerCommandRepository : BaseEfCommandRepository<Customer>, ICustomerCommandRepository
{
    public CustomerCommandRepository(ContosoDbContext db, ILogger<CustomerCommandRepository> logger)
        : base(db, logger)
    {
    }
}
