namespace OrderProcess.Persistence.Abstractions.Entities;

/// <summary>
/// Product/Item catalog entity.
///
/// The upstream contract calls this an "Item" and provides a ProductId string.
/// Persisted model keeps an internal bigint identity and stores the upstream id
/// in <see cref="ExternalProductId"/>.
/// </summary>
public sealed class Product
{
    public long Id { get; set; }

    public string ExternalProductId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Demo-friendly classification.
    /// Examples: Billing, Expenses, Tax, Reporting, Collaboration.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Monthly / Annual (kept as string for demo simplicity).
    /// </summary>
    public string BillingPeriod { get; set; } = "Monthly";

    public bool IsSubscription { get; set; } = true;

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Set by the database (DEFAULT SYSUTCDATETIME()).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Set by the database (DEFAULT SYSUTCDATETIME()) and maintained by a DB trigger on UPDATE.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
