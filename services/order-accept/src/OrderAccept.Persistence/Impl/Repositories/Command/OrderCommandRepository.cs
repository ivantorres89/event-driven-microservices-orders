using Microsoft.Extensions.Logging;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories;
using OrderAccept.Persistence.Abstractions.Repositories.Command;
using OrderAccept.Persistence.Impl.Repositories.Base;

namespace OrderAccept.Persistence.Impl.Repositories.Command;

public sealed class OrderCommandRepository : BaseEfCommandRepository<Order>, IOrderCommandRepository
{
    public OrderCommandRepository(ContosoDbContext db, ILogger<OrderCommandRepository> logger)
        : base(db, logger)
    {
    }
}
