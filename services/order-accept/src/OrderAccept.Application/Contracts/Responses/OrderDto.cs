namespace OrderAccept.Application.Contracts.Responses;

public sealed record OrderDto(
    long Id,
    string CorrelationId,
    DateTime CreatedAt,
    IReadOnlyCollection<OrderItemDto> Items
);
