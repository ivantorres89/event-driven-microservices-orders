using OrderAccept.Application.Contracts.Requests;
using OrderAccept.Shared.Correlation;

namespace OrderAccept.Application.Contracts.Events
{
    /// <summary>
    /// Represents an event indicating that an order has been accepted.
    /// </summary>
    /// <param name="CorrelationId">The unique identifier used to correlate this event with related operations or requests.</param>
    /// <param name="Order">The details of the order that has been accepted.</param>
    public sealed record OrderAcceptedEvent(
        CorrelationId CorrelationId,
        CreateOrderRequest Order
    );
}
