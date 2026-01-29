namespace OrderAccept.Infrastructure.Workflow;

public sealed class WorkflowStateOptions
{
    public const string SectionName = "WorkflowState";

    /// <summary>
    /// TTL for transient workflow state in Redis.
    /// </summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(30);
}
