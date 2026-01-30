namespace OrderNotification.Shared.Workflow;

/// <summary>
/// Transient workflow state used for real-time status reporting and reconnection scenarios.
/// Redis is not a system of record; SQL is authoritative.
/// </summary>
public enum OrderWorkflowStatus
{
    Accepted,
    Processing,
    Completed
}
