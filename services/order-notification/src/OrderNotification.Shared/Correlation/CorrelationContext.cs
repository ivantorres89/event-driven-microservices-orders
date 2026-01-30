using OrderNotification.Shared.Correlation;

public static class CorrelationContext
{
    private static readonly AsyncLocal<CorrelationId?> _current = new();

    public static CorrelationId? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}