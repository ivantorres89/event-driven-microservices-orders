using Microsoft.Extensions.Logging;
using OrderNotification.Application.Abstractions;
using OrderNotification.Application.Commands;
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

            var userId = await _correlationRegistry.ResolveUserIdAsync(command.Event.CorrelationId, cancellationToken);
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogInformation("No user mapping found for CorrelationId={CorrelationId}. Skipping notification.", command.Event.CorrelationId);
                return;
            }

            await _notifier.NotifyStatusChangedAsync(
                userId: userId,
                correlationId: command.Event.CorrelationId,
                status: OrderWorkflowStatus.Completed,
                orderId: command.Event.OrderId,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Pushed OrderProcessed notification to UserId={UserId}", userId);
        }
    }
}
