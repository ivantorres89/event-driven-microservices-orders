using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal sealed class OrderItemRepository : IOrderItemRepository
{
    private readonly ContosoDbContext _db;

    public OrderItemRepository(ContosoDbContext db) => _db = db;

    public void Add(OrderItem item) => _db.OrderItems.Add(item);
}
