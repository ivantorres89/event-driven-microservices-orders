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
        int? offset,
        int? size,
        CancellationToken cancellationToken = default)
    {
        // Per requirements: application decides, and always uses AsNoTracking for GETs.
        const bool asNoTracking = true;

        if (size is null)
        {
            var all = await _uow.ProductQueries.GetAllAsync(asNoTracking, cancellationToken);
            var dtos = _mapper.Map<IReadOnlyCollection<ProductDto>>(all);
            return new PagedResult<ProductDto>(dtos, Offset: 0, Size: dtos.Count, TotalCount: dtos.Count);
        }

        var safeOffset = Math.Max(0, offset ?? 0);
        var safeSize = Math.Max(1, size.Value);

        var total = await _uow.ProductQueries.CountAsync(asNoTracking, cancellationToken);
        var items = await _uow.ProductQueries.GetPagedAsync(safeOffset, safeSize, asNoTracking, cancellationToken);

        var mapped = _mapper.Map<IReadOnlyCollection<ProductDto>>(items);
        return new PagedResult<ProductDto>(mapped, safeOffset, safeSize, total);
    }
}
