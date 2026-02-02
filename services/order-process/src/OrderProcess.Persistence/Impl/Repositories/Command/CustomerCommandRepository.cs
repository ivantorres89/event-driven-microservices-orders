using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Persistence.Impl.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Command;

public sealed class CustomerCommandRepository : BaseEfCommandRepository<Customer>, ICustomerCommandRepository
{
    public CustomerCommandRepository(ContosoDbContext db, ILogger<CustomerCommandRepository> logger)
        : base(db, logger)
    {
    }
}
