using Microsoft.EntityFrameworkCore;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal sealed class OrderRepository : BaseEfRepository<Order>, IOrderRepository
{
    public OrderRepository(ContosoDbContext db) : base(db) { }

    public Task<Order?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => Db.Orders.SingleOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);
}
