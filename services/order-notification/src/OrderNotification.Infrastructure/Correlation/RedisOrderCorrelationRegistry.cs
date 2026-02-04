using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderNotification.Application.Abstractions;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Observability;
using OrderNotification.Shared.Resilience;
using OrderNotification.Shared.Workflow;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StackExchange.Redis;

namespace OrderNotification.Infrastructure.Correlation;

/// <summary>
/// Redis-backed mapping CorrelationId -> UserId used for SignalR routing.
/// Key: order:map:{CorrelationId}
/// TTL aligned with the workflow TTL.
/// </summary>
public sealed class RedisOrderCorrelationRegistry : IOrderCorrelationRegistry
{
    private readonly IConnectionMultiplexer _redis;
    private readonly WorkflowStateOptions _options;
    private readonly ILogger<RedisOrderCorrelationRegistry> _logger;
    private readonly IAsyncPolicy _redisPolicy;

    public RedisOrderCorrelationRegistry(
        IConnectionMultiplexer redis,
        IOptions<WorkflowStateOptions> options,
        ILogger<RedisOrderCorrelationRegistry> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;

        // Identical Redis resilience policy as the workflow store.
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

    public async Task RegisterAsync(CorrelationId correlationId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required", nameof(userId));

        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderUserMap(correlationId);

        using var activity = Observability.ActivitySource.StartActivity("Redis SET", ActivityKind.Client);
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "SET");
        activity?.SetTag("db.statement", $"SET {key}");
        activity?.SetTag("db.redis.key", key);

        try
        {
            await _redisPolicy.ExecuteAsync(async ct =>
            {
                await db.StringSetAsync(key, userId, _options.Ttl);
            }, cancellationToken);

            _logger.LogInformation("Registered correlation mapping in Redis: {Key} -> {UserId} (TTL={Ttl})", key, userId, _options.Ttl);
        }
        catch (BrokenCircuitException ex)
        {
            throw new DependencyUnavailableException("Redis circuit is open. Correlation registry cannot be updated.", ex);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or TimeoutException)
        {
            throw new DependencyUnavailableException("Redis is unavailable. Correlation registry cannot be updated.", ex);
        }
    }

    public async Task<string?> ResolveUserIdAsync(CorrelationId correlationId, CancellationToken cancellationToken = default)
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
            RedisValue raw = default;
            await _redisPolicy.ExecuteAsync(async ct =>
            {
                raw = await db.StringGetAsync(key);
            }, cancellationToken);

            if (!raw.IsNullOrEmpty)
                return raw.ToString();

            // Backwards-compatible read: older clients previously registered under ws:session:{CorrelationId}.
            // If found, "heal" by copying to the canonical key.
            var legacyKey = LegacySessionKey(correlationId);
            RedisValue legacyRaw = default;
            await _redisPolicy.ExecuteAsync(async ct =>
            {
                legacyRaw = await db.StringGetAsync(legacyKey);
            }, cancellationToken);

            if (legacyRaw.IsNullOrEmpty)
                return null;

            var legacyUserId = legacyRaw.ToString();
            await _redisPolicy.ExecuteAsync(async ct =>
            {
                await db.StringSetAsync(key, legacyUserId, _options.Ttl);
            }, cancellationToken);

            return legacyUserId;
        }
        catch (BrokenCircuitException ex)
        {
            throw new DependencyUnavailableException("Redis circuit is open. Correlation registry cannot be read.", ex);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or TimeoutException)
        {
            throw new DependencyUnavailableException("Redis is unavailable. Correlation registry cannot be read.", ex);
        }
    }

    private static string LegacySessionKey(CorrelationId correlationId) => $"ws:session:{correlationId}";
}
