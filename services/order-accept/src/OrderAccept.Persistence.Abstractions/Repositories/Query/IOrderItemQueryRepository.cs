using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Base;

namespace OrderAccept.Persistence.Abstractions.Repositories.Query;

public interface IOrderItemQueryRepository : IQueryRepository<OrderItem>
{
}
