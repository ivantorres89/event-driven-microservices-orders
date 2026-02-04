namespace OrderAccept.Application.Contracts.Responses;

public sealed record OrderItemDto(
    string ProductId,
    string ProductName,
    string ImageUrl,
    decimal UnitPrice,
    int Quantity
);
