using OrderAccept.Application.Abstractions;
using OrderAccept.Shared.Correlation;

public class CorrelationIdProvider : ICorrelationIdProvider
{
    public CorrelationId GetCorrelationId()
    {
        if (CorrelationContext.Current != null)
        {
            return CorrelationContext.Current.Value;
        }

        var newId = CorrelationId.New();
        CorrelationContext.Current = newId;
        return newId;
    }
}