using OrderProcess.Application.Abstractions;
using OrderProcess.Shared.Correlation;

namespace OrderProcess.Infrastructure.Services;

public class CorrelationIdProvider : ICorrelationIdProvider
{
    public CorrelationId GetCorrelationId()
    {
        if (CorrelationContext.Current is null)
        {
            throw new InvalidOperationException("CorrelationContext.Current is not set. The inbound listener must set it from the message payload before invoking handlers.");
        }

        return CorrelationContext.Current.Value;
    }
}