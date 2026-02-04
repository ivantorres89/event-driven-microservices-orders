using Microsoft.Extensions.Logging;
using OrderNotification.Application.Abstractions;
using OrderNotification.Application.Commands;
using OrderNotification.Application.Exceptions;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.Application.Handlers;

/// <summary>
/// Handles inbound OrderProcessed integration events and pushes real-time notifications to the owning user.
/// </summary>
public sealed class NotifyOrderProcessedHandler : INotifyOrderProcessedHandler
{
    private readonly IOrderCorrelationRegistry _correlationRegistry;
    private readonly IOrderStatusNotifier _notifier;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<NotifyOrderProcessedHandler> _logger;

    public NotifyOrderProcessedHandler(
        IOrderCorrelationRegistry correlationRegistry,
        IOrderStatusNotifier notifier,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<NotifyOrderProcessedHandler> logger)
    {
        _correlationRegistry = correlationRegistry;
        _notifier = notifier;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    public async Task HandleAsync(NotifyOrderProcessedCommand command, CancellationToken cancellationToken = default)
    {
        var corr = _correlationIdProvider.GetCorrelationId().ToString();

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = corr
        }))
        {
            _logger.LogInformation("Handling OrderProcessed event. OrderId={OrderId}", command.Event.OrderId);

            // The mapping is expected to be written by order-accept at accept-time.
            // Still, Redis is ephemeral and propagation isn't guaranteed, so we do a short local retry.
            var userId = await ResolveUserIdWithRetryAsync(command.Event.CorrelationId, cancellationToken);

            await _notifier.NotifyStatusChangedAsync(
                userId: userId,
                correlationId: command.Event.CorrelationId,
                status: OrderWorkflowStatus.Completed,
                orderId: command.Event.OrderId,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Pushed OrderProcessed notification to UserId={UserId}", userId);
        }
    }

    private async Task<string> ResolveUserIdWithRetryAsync(OrderNotification.Shared.Correlation.CorrelationId correlationId, CancellationToken cancellationToken)
    {
        // Tight retry to handle rare races (e.g., Redis replication lag) without blocking the broker listener.
        var delays = new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(500)
        };

        for (var attempt = 0; attempt <= delays.Length; attempt++)
        {
            var userId = await _correlationRegistry.ResolveUserIdAsync(correlationId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(userId))
                return userId;

            if (attempt == delays.Length)
                break;

            _logger.LogWarning(
                "Missing correlation mapping for CorrelationId={CorrelationId}. Retry {Attempt}/{Max} in {Delay}.",
                correlationId,
                attempt + 1,
                delays.Length,
                delays[attempt]);

            await Task.Delay(delays[attempt], cancellationToken);
        }

        // Throw to trigger transport-level retry/DLQ, rather than silently losing notifications.
        throw new CorrelationMappingNotFoundException(correlationId.ToString());
    }
}
