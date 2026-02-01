namespace OrderProcess.Persistence.Abstractions.Entities;

public sealed class Order
{
    public long Id { get; set; }

    /// <summary>
    /// CorrelationId from the asynchronous workflow.
    /// Used for idempotency and traceability.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    public long CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>
    /// Set by the database (DEFAULT SYSUTCDATETIME()).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Set by the database (DEFAULT SYSUTCDATETIME()) and maintained by a DB trigger on UPDATE.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
