using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Abstractions;

public interface IGetProductByIdHandler
{
    Task<ProductDto?> HandleAsync(long id, CancellationToken cancellationToken = default);
}
