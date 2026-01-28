using OrderAccept.Application.Contracts.Requests;

namespace OrderAccept.Application.Commands
{
    /// <summary>
    /// Represents a command to accept and process a new order request.
    /// </summary>
    /// <param name="Order">The order request to be accepted and processed. Cannot be null.</param>
    public sealed record AcceptOrderCommand(CreateOrderRequest Order);
}
