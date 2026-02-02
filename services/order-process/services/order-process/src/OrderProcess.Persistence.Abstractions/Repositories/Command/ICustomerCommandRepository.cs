using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories.Command;

public interface ICustomerCommandRepository : ICommandRepository<Customer>
{
}