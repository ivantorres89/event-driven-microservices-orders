using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Base;

namespace OrderAccept.Persistence.Abstractions.Repositories.Query;

public interface IOrderQueryRepository : IQueryRepository<Order>
{
    Task<Order?> GetByCorrelationIdAsync(
        string correlationId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByCustomerIdPagedAsync(
        long customerId,
        int offset,
        int size,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task<int> CountByCustomerIdAsync(
        long customerId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);
}
