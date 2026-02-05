using AutoMapper;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Handlers;

public sealed class GetOrderByIdHandler : IGetOrderByIdHandler
{
    private readonly IContosoUnitOfWork _uow;
    private readonly IMapper _mapper;

    public GetOrderByIdHandler(IContosoUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<OrderDto?> HandleAsync(
        string externalCustomerId,
        long orderId,
        CancellationToken cancellationToken = default)
    {
        const bool asNoTracking = true;

        if (orderId < 1 || string.IsNullOrWhiteSpace(externalCustomerId))
            return null;

        var customer = await _uow.CustomerQueries
            .GetByExternalIdAsync(externalCustomerId, asNoTracking, cancellationToken);

        if (customer is null)
            return null;

        var order = await _uow.OrderQueries.FindAsync(orderId, asNoTracking, cancellationToken);
        if (order is null)
            return null;

        if (order.CustomerId != customer.Id)
            return null;

        return _mapper.Map<OrderDto>(order);
    }
}
