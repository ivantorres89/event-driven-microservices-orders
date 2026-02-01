using Microsoft.EntityFrameworkCore;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly ContosoDbContext _db;

    public OrderRepository(ContosoDbContext db) => _db = db;

    public Task<Order?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => _db.Orders.SingleOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);

    public void Add(Order order) => _db.Orders.Add(order);
}
