using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories.Query;
using OrderProcess.Persistence.Impl.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Query;

public sealed class OrderQueryRepository : BaseEfQueryRepository<Order>, IOrderQueryRepository
{
    public OrderQueryRepository(ContosoDbContext db, ILogger<OrderQueryRepository> logger)
        : base(db, logger)
    {
    }

    protected override IQueryable<Order> Queryable =>
        Db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product);

    public Task<Order?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(o => o.CorrelationId == correlationId, cancellationToken);
}
