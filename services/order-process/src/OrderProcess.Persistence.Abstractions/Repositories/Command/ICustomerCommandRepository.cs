using OrderProcess.Domain.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Base;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface ICustomerCommandRepository : ICommandRepository<Customer>
{
}
