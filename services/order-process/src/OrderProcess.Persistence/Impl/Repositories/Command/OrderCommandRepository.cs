using Microsoft.Extensions.Logging;
using OrderProcess.Domain.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Persistence.Impl.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Command;

public sealed class OrderCommandRepository : BaseEfCommandRepository<Order>, IOrderCommandRepository
{
    public OrderCommandRepository(ContosoDbContext db, ILogger<OrderCommandRepository> logger)
        : base(db, logger)
    {
    }
}
