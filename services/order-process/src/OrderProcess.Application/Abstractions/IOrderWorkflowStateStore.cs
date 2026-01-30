using OrderProcess.Shared.Correlation;
using OrderProcess.Shared.Workflow;

namespace OrderProcess.Application.Abstractions;

/// <summary>
/// Stores transient order workflow state for correlation-based status tracking.
/// This is not a system of record and is expected to be TTL-based.
/// </summary>
public interface IOrderWorkflowStateStore
{
    /// <summary>
    /// Sets the workflow status for an order associated with the specified correlation ID.
    /// The status is stored transiently in Redis with an automatic TTL expiration.
    /// </summary>
    /// <param name="correlationId">The correlation ID that uniquely identifies the order workflow.</param>
    /// <param name="status">The workflow status to set for the order (e.g., Accepted, Processing, Completed).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation of setting the workflow status.</returns>
    Task SetStatusAsync(
        CorrelationId correlationId,
        OrderWorkflowStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes the status associated with the specified correlation identifier.
    /// </summary>
    /// <param name="correlationId">The correlation identifier for which the status should be removed. Must not be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    Task RemoveStatusAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default);
}
