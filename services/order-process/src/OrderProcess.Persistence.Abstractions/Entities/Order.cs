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

    public DateTime CreatedUtc { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
