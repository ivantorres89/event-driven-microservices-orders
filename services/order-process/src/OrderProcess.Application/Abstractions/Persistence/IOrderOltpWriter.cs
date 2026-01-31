using OrderProcess.Application.Contracts.Events;

namespace OrderProcess.Application.Abstractions.Persistence;

/// <summary>
/// Persists an order using an OLTP transaction and returns the generated business identity (OrderId).
/// In the next iteration this will be implemented via EF Core + SQL Server/Azure SQL.
/// </summary>
public interface IOrderOltpWriter
{
    Task<PersistedOrder> PersistAsync(OrderAcceptedEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of persisting an order.
/// </summary>
public sealed record PersistedOrder(long OrderId);
