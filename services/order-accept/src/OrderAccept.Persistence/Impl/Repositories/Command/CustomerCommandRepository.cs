using Microsoft.Extensions.Logging;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories;
using OrderAccept.Persistence.Abstractions.Repositories.Command;
using OrderAccept.Persistence.Impl.Repositories.Base;

namespace OrderAccept.Persistence.Impl.Repositories.Command;

public sealed class CustomerCommandRepository : BaseEfCommandRepository<Customer>, ICustomerCommandRepository
{
    public CustomerCommandRepository(ContosoDbContext db, ILogger<CustomerCommandRepository> logger)
        : base(db, logger)
    {
    }
}
