using OrderAccept.Application.Contracts.Requests;

namespace OrderAccept.Application.Commands
{
    /// <summary>
    /// Represents a command to accept an order.
    /// </summary>
    /// <param name="Order">The order request containing customer and item details.</param>
    /// <param name="CorrelationId">The correlation identifier used to track this operation across the system.</param>
    public record AcceptOrderCommand(CreateOrderRequest Order);
}
