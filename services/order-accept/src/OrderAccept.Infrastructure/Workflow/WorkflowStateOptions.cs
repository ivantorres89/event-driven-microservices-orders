namespace OrderAccept.Infrastructure.Workflow;

public sealed class WorkflowStateOptions
{
    public const string SectionName = "WorkflowState";

    /// <summary>
    /// Time-to-live for transient workflow state keys stored in Redis.
    /// </summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(30);
}
