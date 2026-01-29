using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderAccept.Application.Abstractions;
using OrderAccept.Shared.Correlation;
using OrderAccept.Shared.Workflow;
using StackExchange.Redis;

namespace OrderAccept.Infrastructure.Workflow;

public sealed class RedisOrderWorkflowStateStore : IOrderWorkflowStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly WorkflowStateOptions _options;
    private readonly ILogger<RedisOrderWorkflowStateStore> _logger;

    public RedisOrderWorkflowStateStore(
        IConnectionMultiplexer redis,
        IOptions<WorkflowStateOptions> options,
        ILogger<RedisOrderWorkflowStateStore> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SetStatusAsync(CorrelationId correlationId, OrderWorkflowStatus status, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderStatus(correlationId);

        // Note: StackExchange.Redis does not accept CancellationToken, but we keep it in the contract.
        await db.StringSetAsync(key, status.ToString().ToUpperInvariant(), _options.Ttl);

        _logger.LogInformation("Set workflow status in Redis: {Key}={Status} (TTL={Ttl})", key, status, _options.Ttl);
    }

    public async Task RemoveStatusAsync(CorrelationId correlationId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderStatus(correlationId);

        await db.KeyDeleteAsync(key);
        _logger.LogInformation("Removed workflow status key from Redis: {Key}", key);
    }
}
