using OrderAccept.Domain.Entities.Base;

namespace OrderAccept.Domain.Entities;

public sealed class Order : EntityBase
{
    /// <summary>
    /// CorrelationId from the asynchronous workflow.
    /// Used for idempotency and traceability.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier for the customer.
    /// </summary>
    public long CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer associated with this entity.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// Items included in the order.
    /// </summary>
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
