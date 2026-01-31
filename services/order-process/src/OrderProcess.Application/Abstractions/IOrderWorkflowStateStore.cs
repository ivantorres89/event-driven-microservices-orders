using OrderProcess.Shared.Correlation;
using OrderProcess.Shared.Workflow;

namespace OrderProcess.Application.Abstractions;

/// <summary>
/// Stores transient order workflow state for correlation-based status tracking.
/// This is not a system of record and is expected to be TTL-based.
/// </summary>
public interface IOrderWorkflowStateStore
{
    Task SetStatusAsync(
        CorrelationId correlationId,
        OrderWorkflowStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets workflow state to COMPLETED and also stores the generated OrderId.
    /// Value format is: 'COMPLETED|{OrderId}' for backwards-compatible prefix matching.
    /// </summary>
    Task SetCompletedAsync(
        CorrelationId correlationId,
        long orderId,
        CancellationToken cancellationToken = default);

    Task RemoveStatusAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default);
}
