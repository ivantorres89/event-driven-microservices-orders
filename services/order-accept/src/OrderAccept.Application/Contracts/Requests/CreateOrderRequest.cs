namespace OrderAccept.Application.Contracts.Requests
{
    public sealed record CreateOrderRequest(
        string CustomerId,
        IReadOnlyCollection<CreateOrderItem> Items
    );

    public sealed record CreateOrderItem(
        string ProductId,
        int Quantity
    );
}
