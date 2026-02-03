using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.Application.Abstractions;

/// <summary>
/// Stores transient order workflow state for correlation-based status tracking.
/// This is not a system of record and is expected to be TTL-based.
/// </summary>
public interface IOrderWorkflowStateStore
{
    /// <summary>
    /// Updates workflow status only if the Redis key already exists.
    /// This is useful for downstream processors: we don't want to "resurrect" a workflow that has already expired by TTL.
    /// Returns true if the key existed and was updated.
    /// </summary>
    Task<bool> TrySetStatusIfExistsAsync(
        CorrelationId correlationId,
        OrderWorkflowStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates workflow state to COMPLETED only if the Redis key already exists.
    /// Returns true if the key existed and was updated.
    /// </summary>
    Task<bool> TrySetCompletedIfExistsAsync(
        CorrelationId correlationId,
        long orderId,
        CancellationToken cancellationToken = default);

    Task RemoveStatusAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default);
}
