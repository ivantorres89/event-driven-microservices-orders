namespace OrderAccept.Application.Contracts.Responses;

public sealed record OrderItemDto(
    long Id,
    long ProductId,
    int Quantity,
    ProductDto? Product
);
