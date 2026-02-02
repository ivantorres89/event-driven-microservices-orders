namespace OrderProcess.Persistence.Abstractions.Entities;

/// <summary>
/// Product/Item catalog entity.
///
/// The upstream contract calls this an "Item" and provides a ProductId string.
/// Persisted model keeps an internal bigint identity and stores the upstream id
/// in <see cref="ExternalProductId"/>.
/// </summary>
public sealed class Product : EntityBase
{
    public string ExternalProductId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
