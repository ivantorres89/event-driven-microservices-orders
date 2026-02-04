using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Query;
using OrderAccept.Persistence.Impl.Repositories.Base;

namespace OrderAccept.Persistence.Impl.Repositories.Query;

public sealed class OrderQueryRepository : BaseEfQueryRepository<Order>, IOrderQueryRepository
{
    public OrderQueryRepository(ContosoDbContext db, ILogger<OrderQueryRepository> logger)
        : base(db, logger)
    {
    }

    protected override IQueryable<Order> Queryable(bool asNoTracking)
    {
        // Contract rules:
        // - Orders: IsSoftDeleted = 0
        // - OrderItems: IsSoftDeleted = 0
        var query = Db.Orders
            .Where(o => !o.IsSoftDeleted)
            .Include(o => o.Customer)
            .Include(o => o.Items.Where(i => !i.IsSoftDeleted))
                .ThenInclude(i => i.Product);

        return asNoTracking ? query.AsNoTracking() : query;
    }

    public Task<Order?> GetByCorrelationIdAsync(
        string correlationId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(o => o.CorrelationId == correlationId, asNoTracking, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetByCustomerIdPagedAsync(
        long customerId,
        int offset,
        int size,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        return await Queryable(asNoTracking)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip(offset)
            .Take(size)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByCustomerIdAsync(
        long customerId,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
        => CountAsync(o => o.CustomerId == customerId, asNoTracking, cancellationToken);
}
