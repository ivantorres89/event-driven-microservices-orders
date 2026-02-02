using OrderProcess.Domain.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Base;

namespace OrderProcess.Persistence.Abstractions.Repositories.Query;

public interface IOrderQueryRepository : IQueryRepository<Order>
{
    Task<Order?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
}
