using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Base;

namespace OrderAccept.Persistence.Abstractions.Repositories.Command;

public interface ICustomerCommandRepository : ICommandRepository<Customer>
{
}
