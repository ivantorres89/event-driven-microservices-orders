using Microsoft.Extensions.Options;
using OrderAccept.Application.Abstractions;
using OrderAccept.Shared.Correlation;
using OrderAccept.Shared.Workflow;
using StackExchange.Redis;

namespace OrderAccept.Infrastructure.Workflow;

public sealed class RedisOrderWorkflowStateStore : IOrderWorkflowStateStore
{
    private readonly IDatabase _db;
    private readonly TimeSpan _ttl;

    public RedisOrderWorkflowStateStore(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<WorkflowStateOptions> options)
    {
        _db = connectionMultiplexer.GetDatabase();
        _ttl = options.Value.Ttl;
    }

    public Task SetStatusAsync(
        CorrelationId correlationId,
        OrderWorkflowStatus status,
        CancellationToken cancellationToken = default)
    {
        // StackExchange.Redis does not accept CancellationToken; we keep the signature for consistency. 
        var key = WorkflowRedisKeys.OrderStatus(correlationId);
        var value = status.ToString().ToUpperInvariant();
        return _db.StringSetAsync(key, value, _ttl);
    }

    public Task RemoveStatusAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default)
    {
        var key = WorkflowRedisKeys.OrderStatus(correlationId);
        return _db.KeyDeleteAsync(key);
    }
}
