namespace OrderProcess.Persistence.Abstractions.Entities;

public sealed class OrderItem
{
    public long Id { get; set; }

    public long OrderId { get; set; }
    public Order? Order { get; set; }

    public long ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }

    /// <summary>
    /// Set by the database (DEFAULT SYSUTCDATETIME()).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Set by the database (DEFAULT SYSUTCDATETIME()) and maintained by a DB trigger on UPDATE.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
