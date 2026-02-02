using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Base;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IOrderItemCommandRepository : ICommandRepository<OrderItem>
{
}
