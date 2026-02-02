using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal sealed class OrderItemRepository : BaseEfRepository<OrderItem>, IOrderItemRepository
{
    public OrderItemRepository(ContosoDbContext db) : base(db) { }
}
