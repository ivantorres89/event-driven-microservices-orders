using OrderProcess.Application.Abstractions;
using OrderProcess.Shared.Correlation;

namespace OrderProcess.Infrastructure.Services;

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