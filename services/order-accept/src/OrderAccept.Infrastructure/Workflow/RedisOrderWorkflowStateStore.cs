using System.Diagnostics;
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

        // Create an explicit client span for Redis so it always shows up in traces,
        // even if automatic StackExchange.Redis instrumentation is not active.
        using var activity = Observability.ActivitySource.StartActivity("Redis SET", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "SET");
        activity?.SetTag("db.statement", $"SET {key}");
        activity?.SetTag("db.redis.key", key);
        activity?.SetTag("workflow.status", status.ToString());

        // Note: StackExchange.Redis does not accept CancellationToken, but we keep it in the contract.
        await db.StringSetAsync(key, status.ToString().ToUpperInvariant(), _options.Ttl);

        _logger.LogInformation("Set workflow status in Redis: {Key}={Status} (TTL={Ttl})", key, status, _options.Ttl);
    }

    public async Task RemoveStatusAsync(CorrelationId correlationId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderStatus(correlationId);

        using var activity = Observability.ActivitySource.StartActivity("Redis DEL", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "DEL");
        activity?.SetTag("db.statement", $"DEL {key}");
        activity?.SetTag("db.redis.key", key);

        await db.KeyDeleteAsync(key);
        _logger.LogInformation("Removed workflow status key from Redis: {Key}", key);
    }
}
