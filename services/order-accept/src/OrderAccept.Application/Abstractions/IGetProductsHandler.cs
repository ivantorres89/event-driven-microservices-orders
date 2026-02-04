using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Abstractions;

public interface IGetProductsHandler
{
    Task<PagedResult<ProductDto>> HandleAsync(int? offset, int? size, CancellationToken cancellationToken = default);
}
