using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
}
