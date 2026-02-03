using OrderNotification.Application.Abstractions;
using OrderNotification.Shared.Correlation;

namespace OrderNotification.Infrastructure.Services;

public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    public CorrelationId GetCorrelationId()
    {
        if (CorrelationContext.Current is null)
        {
            throw new InvalidOperationException(
                "CorrelationContext.Current is not set. The inbound listener must set it from the message payload before invoking handlers.");
        }

        return CorrelationContext.Current.Value;
    }
}
