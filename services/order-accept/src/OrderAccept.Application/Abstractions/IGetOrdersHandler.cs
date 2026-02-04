using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Abstractions;

public interface IGetOrdersHandler
{
    Task<PagedResult<OrderDto>> HandleAsync(
        string externalCustomerId,
        int offset,
        int size,
        CancellationToken cancellationToken = default);
}
