using Microsoft.AspNetCore.SignalR;
using OrderNotification.Application.Abstractions;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;
using OrderNotification.Worker.Hubs;
using OrderNotification.Worker.Hubs.Models;

namespace OrderNotification.Worker.Notifiers;

/// <summary>
/// SignalR implementation of <see cref="IOrderStatusNotifier"/>.
/// Uses Clients.User(userId) so all connections (multiple tabs, multiple pods via Redis backplane) receive the message.
/// </summary>
public sealed class SignalROrderStatusNotifier : IOrderStatusNotifier
{
    private readonly IHubContext<OrderStatusHub, IOrderStatusClient> _hubContext;
    private readonly ILogger<SignalROrderStatusNotifier> _logger;

    public SignalROrderStatusNotifier(
        IHubContext<OrderStatusHub, IOrderStatusClient> hubContext,
        ILogger<SignalROrderStatusNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyStatusChangedAsync(
        string userId,
        CorrelationId correlationId,
        OrderWorkflowStatus status,
        long? orderId,
        CancellationToken cancellationToken = default)
    {
        var message = new OrderStatusNotification(correlationId, status, orderId);

        _logger.LogInformation(
            "Pushing notification to user. UserId={UserId} CorrelationId={CorrelationId} Status={Status} OrderId={OrderId}",
            userId,
            correlationId,
            status,
            orderId);

        await _hubContext.Clients.User(userId).Notification(message);
    }
}
