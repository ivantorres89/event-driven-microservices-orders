using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Base;

namespace OrderProcess.Persistence.Abstractions.Repositories;

public interface IOrderQueryRepository : IQueryRepository<Order>
{
    Task<Order?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
}
