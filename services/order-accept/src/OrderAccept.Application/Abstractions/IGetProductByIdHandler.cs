using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Abstractions;

public interface IGetProductByIdHandler
{
    Task<ProductDto?> HandleAsync(
        string externalProductId,
        CancellationToken cancellationToken = default);
}
