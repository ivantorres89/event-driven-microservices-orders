using AutoMapper;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Handlers;

public sealed class GetProductByIdHandler : IGetProductByIdHandler
{
    private readonly IContosoUnitOfWork _uow;
    private readonly IMapper _mapper;

    public GetProductByIdHandler(IContosoUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<ProductDto?> HandleAsync(
        string externalProductId,
        CancellationToken cancellationToken = default)
    {
        const bool asNoTracking = true;

        if (string.IsNullOrWhiteSpace(externalProductId))
            return null;

        var entity = await _uow.ProductQueries.GetByExternalIdAsync(externalProductId, asNoTracking, cancellationToken);
        return entity is null ? null : _mapper.Map<ProductDto>(entity);
    }
}
