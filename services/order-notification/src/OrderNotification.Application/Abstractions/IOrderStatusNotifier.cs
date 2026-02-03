using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.Application.Abstractions;

/// <summary>
/// Abstraction over the real-time delivery mechanism (SignalR).
/// </summary>
public interface IOrderStatusNotifier
{
    Task NotifyStatusChangedAsync(
        string userId,
        CorrelationId correlationId,
        OrderWorkflowStatus status,
        long? orderId,
        CancellationToken cancellationToken = default);
}
