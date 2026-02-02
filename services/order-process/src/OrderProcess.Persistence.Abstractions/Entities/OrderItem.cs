namespace OrderProcess.Persistence.Abstractions.Entities;

public sealed class OrderItem : EntityBase
{
    public long OrderId { get; set; }
    public Order? Order { get; set; }

    public long ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
}
