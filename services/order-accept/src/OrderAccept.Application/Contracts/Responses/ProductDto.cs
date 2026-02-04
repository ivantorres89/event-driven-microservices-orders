namespace OrderAccept.Application.Contracts.Responses;

/// <summary>
/// Product data contract exposed by the API.
///
/// IMPORTANT: This DTO is intentionally minimal and aligns with the OpenAPI/Markdown contract.
/// </summary>
public sealed record ProductDto(
    string ExternalProductId,
    string Name,
    string Category,
    string Vendor,
    string ImageUrl,
    int Discount,
    string BillingPeriod,
    bool IsSubscription,
    decimal Price
);
