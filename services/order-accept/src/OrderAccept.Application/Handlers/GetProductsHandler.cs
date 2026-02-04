using AutoMapper;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Handlers;

public sealed class GetProductsHandler : IGetProductsHandler
{
    private readonly IContosoUnitOfWork _uow;
    private readonly IMapper _mapper;

    public GetProductsHandler(IContosoUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PagedResult<ProductDto>> HandleAsync(
        int offset,
        int size,
        CancellationToken cancellationToken = default)
    {
        // Per requirements: application decides, and always uses AsNoTracking for GETs.
        const bool asNoTracking = true;

        var total = await _uow.ProductQueries.CountAsync(asNoTracking, cancellationToken);
        if (total == 0)
            return new PagedResult<ProductDto>(offset, size, 0, Array.Empty<ProductDto>());

        var items = await _uow.ProductQueries.GetPagedAsync(offset, size, asNoTracking, cancellationToken);
        var mapped = _mapper.Map<IReadOnlyCollection<ProductDto>>(items);

        return new PagedResult<ProductDto>(offset, size, total, mapped);
    }
}
