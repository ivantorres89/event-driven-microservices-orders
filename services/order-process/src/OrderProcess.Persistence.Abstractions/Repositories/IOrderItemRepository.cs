using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IOrderItemRepository
{
    void Add(OrderItem item);
}
