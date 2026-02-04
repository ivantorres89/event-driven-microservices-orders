namespace OrderAccept.Application.Contracts.Requests;

public sealed record CreateOrderRequest(
    IReadOnlyCollection<CreateOrderItem> Items
);

public sealed record CreateOrderItem(
    string ProductId,
    int Quantity
);
