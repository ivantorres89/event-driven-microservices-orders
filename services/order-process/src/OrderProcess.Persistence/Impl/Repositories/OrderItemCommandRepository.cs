using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

public sealed class OrderItemCommandRepository : BaseEfCommandRepository<OrderItem>, IOrderItemCommandRepository
{
    public OrderItemCommandRepository(ContosoDbContext db, ILogger<OrderItemCommandRepository> logger)
        : base(db, logger)
    {
    }
}
