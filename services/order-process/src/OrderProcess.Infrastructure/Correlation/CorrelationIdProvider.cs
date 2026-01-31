using OrderProcess.Application.Abstractions;
using OrderProcess.Shared.Correlation;

namespace OrderProcess.Infrastructure.Correlation;

/// <summary>
/// Worker correlation provider. The CorrelationId must be set by the inbound message listener
/// (extracted from the message payload) before application handlers run.
/// </summary>
public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    public CorrelationId GetCorrelationId()
    {
        if (CorrelationContext.Current is null)
            throw new InvalidOperationException("CorrelationContext.Current is not set. The inbound listener must set it from the message payload before invoking handlers.");

        return CorrelationContext.Current.Value;
    }
}
