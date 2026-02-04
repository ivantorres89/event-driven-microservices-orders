using OrderAccept.Application.Contracts.Requests;

namespace OrderAccept.Application.Commands;

/// <summary>
/// Represents a command to accept an order.
/// </summary>
/// <param name="ExternalCustomerId">Customer id derived from the JWT (subject claim).</param>
/// <param name="Order">The order request containing item details.</param>
public sealed record AcceptOrderCommand(
    string ExternalCustomerId,
    CreateOrderRequest Order
);
