using AutoMapper;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Handlers;

public sealed class GetOrdersHandler : IGetOrdersHandler
{
    private readonly IContosoUnitOfWork _uow;
    private readonly IMapper _mapper;

    public GetOrdersHandler(IContosoUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<PagedResult<OrderDto>> HandleAsync(
        string externalCustomerId,
        int offset,
        int size,
        CancellationToken cancellationToken = default)
    {
        const bool asNoTracking = true;

        if (string.IsNullOrWhiteSpace(externalCustomerId))
            return new PagedResult<OrderDto>(offset, size, 0, Array.Empty<OrderDto>());

        var customer = await _uow.CustomerQueries.GetByExternalIdAsync(externalCustomerId, asNoTracking, cancellationToken);
        if (customer is null)
            return new PagedResult<OrderDto>(offset, size, 0, Array.Empty<OrderDto>());

        var total = await _uow.OrderQueries.CountByCustomerIdAsync(customer.Id, asNoTracking, cancellationToken);
        if (total == 0)
            return new PagedResult<OrderDto>(offset, size, 0, Array.Empty<OrderDto>());

        var orders = await _uow.OrderQueries.GetByCustomerIdPagedAsync(customer.Id, offset, size, asNoTracking, cancellationToken);
        var mapped = _mapper.Map<IReadOnlyCollection<OrderDto>>(orders);

        return new PagedResult<OrderDto>(offset, size, total, mapped);
    }
}
