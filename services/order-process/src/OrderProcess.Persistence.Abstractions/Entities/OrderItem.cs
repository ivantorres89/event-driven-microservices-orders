namespace OrderProcess.Persistence.Abstractions.Entities;

public sealed class OrderItem : EntityBase
{
    /// <summary>
    /// Gets or sets the unique identifier for the order.
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// Gets or sets the order associated with this entity.
    /// </summary>
    public Order? Order { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the product.
    /// </summary>
    public long ProductId { get; set; }

    /// <summary>
    /// Gets or sets the product associated with this entity.
    /// </summary>
    public Product? Product { get; set; }

    /// <summary>
    /// Gets or sets the quantity associated with the item.
    /// </summary>
    public int Quantity { get; set; }
}
