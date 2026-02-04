namespace OrderAccept.Application.Contracts.Responses;

/// <summary>
/// Product data contract exposed by the API.
/// </summary>
public sealed record ProductDto(
    long Id,
    string ExternalProductId,
    string Name,
    string Category,
    string BillingPeriod,
    bool IsSubscription,
    decimal Price,
    bool IsActive
);
