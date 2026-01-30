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

        // --- Resilience policy (Redis) ---
        // - 2 retries with backoff for transient issues
        // - Circuit breaker to avoid saturating Redis when unhealthy
        // Redis is critical for workflow traceability; on exhaustion -> throw DependencyUnavailableException.

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

        // We configure a breaker to behave like
        // "open after 5 failed operations" within a rolling window.
        // - failureThreshold: 1.0 => opens only if 100% of calls in the sample fail
        // - minimumThroughput: 5 => requires at least 5 calls before it can open
        // - samplingDuration: 30s => sample window
        // This yields a practical "5 failures then open" behaviour under sustained failure.

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

        try
        {
            // Note: StackExchange.Redis does not accept CancellationToken, but we keep it in the contract.
            // Polly timeout is pessimistic; it will interrupt the awaited operation if it exceeds the limit.
            await _redisPolicy.ExecuteAsync(async ct =>
            {
                await db.StringSetAsync(key, status.ToString().ToUpperInvariant(), _options.Ttl);
            }, cancellationToken);

            _logger.LogInformation("Set workflow status in Redis: {Key}={Status} (TTL={Ttl})", key, status, _options.Ttl);
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
            // Best-effort cleanup. We don't rethrow to avoid masking the original failure in callers.
            _logger.LogWarning(ex, "Redis circuit open; could not remove workflow status key: {Key}", key);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or TimeoutException)
        {
            // Best-effort cleanup.
            _logger.LogWarning(ex, "Failed to remove workflow status key from Redis: {Key}", key);
        }
    }
}
