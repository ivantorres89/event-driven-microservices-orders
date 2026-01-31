using OrderProcess.Application.Contracts.Requests;
using OrderProcess.Shared.Correlation;

namespace OrderProcess.Application.Contracts.Events;

/// <summary>
/// Integration event published by order-accept.
/// </summary>
public sealed record OrderAcceptedEvent(
    CorrelationId CorrelationId,
    CreateOrderRequest Order
);
