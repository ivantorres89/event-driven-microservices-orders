using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderProcess.Domain.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Query;
using OrderProcess.Persistence.Impl.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Query;

public sealed class OrderItemQueryRepository : BaseEfQueryRepository<OrderItem>, IOrderItemQueryRepository
{
    public OrderItemQueryRepository(ContosoDbContext db, ILogger<OrderItemQueryRepository> logger)
        : base(db, logger)
    {
    }

    protected override IQueryable<OrderItem> Queryable =>
        Db.OrderItems
            .AsNoTracking()
            .Include(i => i.Product);
}
