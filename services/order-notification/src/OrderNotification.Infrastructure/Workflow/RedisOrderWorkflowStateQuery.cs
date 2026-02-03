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

public sealed class RedisOrderWorkflowStateQuery : IOrderWorkflowStateQuery
{
    private readonly IConnectionMultiplexer _redis;
    private readonly WorkflowStateOptions _options;
    private readonly ILogger<RedisOrderWorkflowStateQuery> _logger;
    private readonly IAsyncPolicy _redisPolicy;

    public RedisOrderWorkflowStateQuery(
        IConnectionMultiplexer redis,
        IOptions<WorkflowStateOptions> options,
        ILogger<RedisOrderWorkflowStateQuery> logger)
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

    public async Task<OrderWorkflowState?> GetAsync(CorrelationId correlationId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = WorkflowRedisKeys.OrderStatus(correlationId);

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

            if (raw.IsNullOrEmpty)
                return null;

            var value = raw.ToString();
            if (value.StartsWith("COMPLETED|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = value.Split('|', 2);
                if (parts.Length == 2 && long.TryParse(parts[1], out var orderId))
                    return new OrderWorkflowState(OrderWorkflowStatus.Completed, orderId);

                return new OrderWorkflowState(OrderWorkflowStatus.Completed, null);
            }

            if (value.Equals("ACCEPTED", StringComparison.OrdinalIgnoreCase))
                return new OrderWorkflowState(OrderWorkflowStatus.Accepted, null);
            if (value.Equals("PROCESSING", StringComparison.OrdinalIgnoreCase))
                return new OrderWorkflowState(OrderWorkflowStatus.Processing, null);
            if (value.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
                return new OrderWorkflowState(OrderWorkflowStatus.Completed, null);

            _logger.LogWarning("Unknown workflow status value in Redis. Key={Key} Value={Value}", key, value);
            return null;
        }
        catch (BrokenCircuitException ex)
        {
            throw new DependencyUnavailableException("Redis circuit is open. Workflow state cannot be read.", ex);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutRejectedException or TimeoutException)
        {
            throw new DependencyUnavailableException("Redis is unavailable. Workflow state cannot be read.", ex);
        }
    }
}
