using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    void Add(Order order);
}
