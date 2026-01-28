namespace OrderAccept.Application.Contracts.Requests
{
    /// <summary>
    /// Represents a request to create a new order for a specified customer with a collection of order items.
    /// </summary>
    /// <param name="CustomerId">The unique identifier of the customer placing the order. Cannot be null or empty.</param>
    /// <param name="Items">The collection of items to include in the order. Must contain at least one item. Cannot be null.</param>
    public sealed record CreateOrderRequest(
        string CustomerId,
        IReadOnlyCollection<CreateOrderItem> Items
    );

    /// <summary>
    /// Represents the data required to create a new item in an order.
    /// </summary>
    /// <param name="ProductId">The unique identifier of the product to add to the order. Cannot be null or empty.</param>
    /// <param name="Quantity">The number of units of the product to add. Must be greater than zero.</param>
    public sealed record CreateOrderItem(
        string ProductId,
        int Quantity
    );
}
