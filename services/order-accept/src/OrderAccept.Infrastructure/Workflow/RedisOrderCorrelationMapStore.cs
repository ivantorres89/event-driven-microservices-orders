using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderAccept.Application.Abstractions;
using OrderAccept.Shared.Correlation;
using OrderAccept.Shared.Resilience;
using OrderAccept.Shared.Workflow;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StackExchange.Redis;

namespace OrderAccept.Infrastructure.Workflow;

/// <summary>
/// Redis-backed implementation of <see cref="IOrderCorrelationMapStore"/>.
/// Stores CorrelationId -> UserId mappings with a TTL so downstream notification
/// services can route real-time messages.
/// </summary>
public sealed class RedisOrderCorrelationMapStore : IOrderCorrelationMapStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly WorkflowStateOptions _options;
    private readonly ILogger<RedisOrderCorrelationMapStore> _logger;
    private readonly IAsyncPolicy _redisPolicy;

    public RedisOrderCorrelationMapStore(
        IConnectionMultiplexer redis,
        IOptions<WorkflowStateOptions> options,
        ILogger<RedisOrderCorrelationMapStore> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;

        // Same resilience profile as RedisOrderWorkflowStateStore:
        // - short pessimistic timeout
        // - small retry for transients
        // - circuit breaker to avoid saturating Redis when unhealthy

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

    public async Task SetUserIdAsync(CorrelationId correlationId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId is required", nameof(userId));

        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderUserMap(correlationId);

        using var activity = Observability.ActivitySource.StartActivity("Redis SET", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "SET");
        activity?.SetTag("db.statement", $"SET {key}");
        activity?.SetTag("db.redis.key", key);
        activity?.SetTag("workflow.correlation_id", correlationId.ToString());

        try
        {
            await _redisPolicy.ExecuteAsync(async ct =>
            {
                await db.StringSetAsync(key, userId, _options.Ttl);
            }, cancellationToken);

            _logger.LogInformation("Set correlation mapping in Redis: {Key}={UserId} (TTL={Ttl})", key, userId, _options.Ttl);
        }
        catch (BrokenCircuitException ex)
        {
            throw new DependencyUnavailableException("Redis circuit is open. Correlation mapping cannot be stored.", ex);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or TimeoutException)
        {
            throw new DependencyUnavailableException("Redis is unavailable. Correlation mapping cannot be stored.", ex);
        }
    }

    public async Task<string?> GetUserIdAsync(CorrelationId correlationId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderUserMap(correlationId);

        using var activity = Observability.ActivitySource.StartActivity("Redis GET", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "GET");
        activity?.SetTag("db.statement", $"GET {key}");
        activity?.SetTag("db.redis.key", key);

        try
        {
            var value = await _redisPolicy.ExecuteAsync(async ct =>
            {
                return await db.StringGetAsync(key);
            }, cancellationToken);

            return value.HasValue ? value.ToString() : null;
        }
        catch (BrokenCircuitException ex)
        {
            throw new DependencyUnavailableException("Redis circuit is open. Correlation mapping cannot be read.", ex);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or TimeoutException)
        {
            throw new DependencyUnavailableException("Redis is unavailable. Correlation mapping cannot be read.", ex);
        }
    }

    public async Task RefreshTtlAsync(CorrelationId correlationId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderUserMap(correlationId);

        using var activity = Observability.ActivitySource.StartActivity("Redis EXPIRE", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "EXPIRE");
        activity?.SetTag("db.statement", $"EXPIRE {key}");
        activity?.SetTag("db.redis.key", key);

        try
        {
            await _redisPolicy.ExecuteAsync(async ct =>
            {
                await db.KeyExpireAsync(key, _options.Ttl);
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is BrokenCircuitException or RedisException or TimeoutRejectedException or TimeoutException)
        {
            // Best-effort refresh.
            _logger.LogWarning(ex, "Failed to refresh TTL for correlation mapping key: {Key}", key);
        }
    }

    public async Task RemoveAsync(CorrelationId correlationId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderUserMap(correlationId);

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

            _logger.LogInformation("Removed correlation mapping key from Redis: {Key}", key);
        }
        catch (Exception ex) when (ex is BrokenCircuitException or RedisException or TimeoutRejectedException or TimeoutException)
        {
            // Best-effort cleanup.
            _logger.LogWarning(ex, "Failed to remove correlation mapping key from Redis: {Key}", key);
        }
    }
}
