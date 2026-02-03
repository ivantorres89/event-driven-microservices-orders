using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.Application.Abstractions;

/// <summary>
/// Read model over the transient workflow state stored in Redis.
/// Used for reconnection / refresh scenarios.
/// </summary>
public interface IOrderWorkflowStateQuery
{
    Task<OrderWorkflowState?> GetAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Current transient workflow state. OrderId is only present when status is Completed.
/// </summary>
public sealed record OrderWorkflowState(OrderWorkflowStatus Status, long? OrderId);
