using Microsoft.Extensions.Logging;
using OrderProcess.Domain.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Persistence.Abstractions.Repositories.Command;
using OrderProcess.Persistence.Impl.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Command;

public sealed class OrderItemCommandRepository : BaseEfCommandRepository<OrderItem>, IOrderItemCommandRepository
{
    public OrderItemCommandRepository(ContosoDbContext db, ILogger<OrderItemCommandRepository> logger)
        : base(db, logger)
    {
    }
}
