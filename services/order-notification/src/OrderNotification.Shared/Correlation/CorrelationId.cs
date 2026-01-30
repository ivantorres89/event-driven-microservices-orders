namespace OrderNotification.Shared.Correlation;

/// <summary>
/// Technical identifier used to correlate logs, traces, messages and transient workflow state across services.
/// </summary>
public readonly record struct CorrelationId(Guid Value)
{
    public static CorrelationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
