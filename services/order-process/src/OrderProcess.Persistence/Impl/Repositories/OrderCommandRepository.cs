using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

public sealed class OrderCommandRepository : BaseEfCommandRepository<Order>, IOrderCommandRepository
{
    public OrderCommandRepository(ContosoDbContext db, ILogger<OrderCommandRepository> logger)
        : base(db, logger)
    {
    }
}
