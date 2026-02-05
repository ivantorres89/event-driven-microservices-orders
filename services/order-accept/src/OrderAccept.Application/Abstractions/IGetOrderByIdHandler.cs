using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Abstractions;

public interface IGetOrderByIdHandler
{
    Task<OrderDto?> HandleAsync(
        string externalCustomerId,
        long orderId,
        CancellationToken cancellationToken = default);
}
