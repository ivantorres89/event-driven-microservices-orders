using OrderAccept.Domain.Entities.Base;

namespace OrderAccept.Domain.Entities;

/// <summary>
/// Product/Item catalog entity.
///
/// The upstream contract calls this an "Item" and provides a ProductId string.
/// Persisted model keeps an internal bigint identity and stores the upstream id
/// in <see cref="ExternalProductId"/>.
/// </summary>
public sealed class Product : EntityBase
{
    /// <summary>
    /// External product identifier from upstream systems.
    /// </summary>
    public string ExternalProductId { get; set; } = string.Empty;

    /// <summary>
    /// Product display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Demo-friendly classification.
    /// Examples: Billing, Expenses, Tax, Reporting, Collaboration.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Vendor / brand displayed to the user.
    /// </summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>
    /// Product image URL displayed by the SPA.
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Percentage discount (0..100).
    /// </summary>
    public int Discount { get; set; }

    /// <summary>
    /// Monthly / Annual (kept as string for demo simplicity).
    /// </summary>
    public string BillingPeriod { get; set; } = "Monthly";

    /// <summary>
    /// Indicates whether the product is a subscription.
    /// </summary>
    public bool IsSubscription { get; set; } = true;

    /// <summary>
    /// Unit price of the product.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Indicates whether the product is active and can be ordered.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Order items that reference this product.
    /// </summary>
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
