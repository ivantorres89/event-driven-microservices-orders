using OrderAccept.Application.Contracts.Requests;
using OrderAccept.Shared.Correlation;

namespace OrderAccept.Application.Contracts.Events;

public sealed record OrderAcceptedEvent(
    CorrelationId CorrelationId,
    CreateOrderRequest Order
);
