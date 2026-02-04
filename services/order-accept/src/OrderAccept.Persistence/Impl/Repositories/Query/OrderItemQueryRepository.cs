using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Query;
using OrderAccept.Persistence.Impl.Repositories.Base;

namespace OrderAccept.Persistence.Impl.Repositories.Query;

public sealed class OrderItemQueryRepository : BaseEfQueryRepository<OrderItem>, IOrderItemQueryRepository
{
    public OrderItemQueryRepository(ContosoDbContext db, ILogger<OrderItemQueryRepository> logger)
        : base(db, logger)
    {
    }

    protected override IQueryable<OrderItem> Queryable(bool asNoTracking)
    {
        var query = Db.OrderItems
            .Where(i => !i.IsSoftDeleted)
            .Include(i => i.Product);

        return asNoTracking ? query.AsNoTracking() : query;
    }
}
