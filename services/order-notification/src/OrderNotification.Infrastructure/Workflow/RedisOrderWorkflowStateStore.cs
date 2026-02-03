using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderNotification.Application.Abstractions;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Observability;
using OrderNotification.Shared.Resilience;
using OrderNotification.Shared.Workflow;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StackExchange.Redis;

namespace OrderNotification.Infrastructure.Workflow;

public sealed class RedisOrderWorkflowStateStore : IOrderWorkflowStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly WorkflowStateOptions _options;
    private readonly ILogger<RedisOrderWorkflowStateStore> _logger;
    private readonly IAsyncPolicy _redisPolicy;

    public RedisOrderWorkflowStateStore(
        IConnectionMultiplexer redis,
        IOptions<WorkflowStateOptions> options,
        ILogger<RedisOrderWorkflowStateStore> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;

        var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(1), TimeoutStrategy.Pessimistic);

        var delays = new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250)
        };

        var retryPolicy = Policy
            .Handle<RedisException>()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: delays.Length,
                sleepDurationProvider: attempt => delays[attempt - 1],
                onRetry: (ex, sleep, attempt, _) =>
                {
                    _logger.LogWarning(ex,
                        "Redis operation failed (attempt {Attempt}/{Max}). Retrying in {Delay}...",
                        attempt,
                        delays.Length,
                        sleep);
                });

        var breakerPolicy = Policy
            .Handle<RedisException>()
            .Or<TimeoutRejectedException>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 1.0,
                samplingDuration: TimeSpan.FromSeconds(30),
                minimumThroughput: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, breakDelay) =>
                {
                    _logger.LogError(ex, "Redis circuit opened for {BreakDelay} due to repeated failures", breakDelay);
                },
                onReset: () => _logger.LogInformation("Redis circuit closed (recovered)"),
                onHalfOpen: () => _logger.LogWarning("Redis circuit half-open; probing Redis")
            );

        _redisPolicy = Policy.WrapAsync(breakerPolicy, retryPolicy, timeoutPolicy);
    }

    public async Task<bool> TrySetStatusIfExistsAsync(CorrelationId correlationId, OrderWorkflowStatus status, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderStatus(correlationId);

        using var activity = Observability.ActivitySource.StartActivity("Redis SET (EXISTS)", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "SET");
        activity?.SetTag("db.statement", $"SET {key} (XX)");
        activity?.SetTag("db.redis.key", key);
        activity?.SetTag("workflow.status", status.ToString());

        try
        {
            var updated = await _redisPolicy.ExecuteAsync(
                async ct => await db.StringSetAsync(
                    key, status.ToString().ToUpperInvariant(),
                    _options.Ttl, when: When.Exists), cancellationToken);

            if (!updated)
                _logger.LogWarning("Workflow key does not exist in Redis (likely expired TTL). Not updating status. Key={Key} Status={Status}", key, status);
            else
                _logger.LogInformation("Updated workflow status in Redis (existing key): {Key}={Status} (TTL={Ttl})", key, status, _options.Ttl);

            return updated;
        }
        catch (BrokenCircuitException ex)
        {
            throw new DependencyUnavailableException("Redis circuit is open. Workflow state cannot be stored.", ex);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or TimeoutException)
        {
            throw new DependencyUnavailableException("Redis is unavailable. Workflow state cannot be stored.", ex);
        }
    }

    public async Task<bool> TrySetCompletedIfExistsAsync(CorrelationId correlationId, long orderId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderStatus(correlationId);
        var value = $"COMPLETED|{orderId}";

        using var activity = Observability.ActivitySource.StartActivity("Redis SET (EXISTS)", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "SET");
        activity?.SetTag("db.statement", $"SET {key} (XX)");
        activity?.SetTag("db.redis.key", key);
        activity?.SetTag("workflow.status", "COMPLETED");
        activity?.SetTag("order.id", orderId);

        try
        {
            var updated = await _redisPolicy.ExecuteAsync(
                async ct => await db.StringSetAsync(
                    key, value, _options.Ttl, when: When.Exists), cancellationToken);

            if (!updated)
                _logger.LogWarning("Workflow key does not exist in Redis (likely expired TTL). Not updating completed status. Key={Key} OrderId={OrderId}", key, orderId);
            else
                _logger.LogInformation("Updated workflow status in Redis (existing key): {Key}={Value} (ttl={Ttl})", key, value, _options.Ttl);

            return updated;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or BrokenCircuitException)
        {
            throw new DependencyUnavailableException("Redis is unavailable. Failed to set workflow state.", ex);
        }
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

        try
        {
            await _redisPolicy.ExecuteAsync(async ct =>
            {
                await db.KeyDeleteAsync(key);
            }, cancellationToken);

            _logger.LogInformation("Removed workflow status key from Redis: {Key}", key);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "Redis circuit open; could not remove workflow status key: {Key}", key);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or TimeoutException)
        {
            _logger.LogWarning(ex, "Failed to remove workflow status key from Redis: {Key}", key);
        }
    }
}
