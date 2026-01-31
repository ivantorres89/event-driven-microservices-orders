using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Application.Contracts.Events;

namespace OrderProcess.Infrastructure.Services;

/// <summary>
/// Temporary OLTP writer for the first iteration. Simulates a SQL PK generation and basic idempotency by CorrelationId.
/// Replace with EF Core + SQL Server/Azure SQL in the next iteration.
/// </summary>
public sealed class InMemoryOrderOltpWriter : IOrderOltpWriter
{
    private static long _nextId = 0;
    private static readonly ConcurrentDictionary<Guid, long> _byCorrelation = new();

    private readonly ILogger<InMemoryOrderOltpWriter> _logger;

    public InMemoryOrderOltpWriter(ILogger<InMemoryOrderOltpWriter> logger)
    {
        _logger = logger;
    }

    public Task<PersistedOrder> PersistAsync(OrderAcceptedEvent @event, CancellationToken cancellationToken = default)
    {
        // Idempotency simulation: same correlation always returns same OrderId.
        var id = _byCorrelation.GetOrAdd(@event.CorrelationId.Value, _ =>
        {
            var newId = Interlocked.Increment(ref _nextId);
            _logger.LogInformation("Simulated OLTP persist for CorrelationId={CorrelationId}. Generated OrderId={OrderId}",
                @event.CorrelationId.Value, newId);
            return newId;
        });

        return Task.FromResult(new PersistedOrder(id));
    }
}
