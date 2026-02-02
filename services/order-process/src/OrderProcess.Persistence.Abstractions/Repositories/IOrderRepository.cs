using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IOrderRepository : IBaseRepository<Order>
{
    Task<Order?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
}
