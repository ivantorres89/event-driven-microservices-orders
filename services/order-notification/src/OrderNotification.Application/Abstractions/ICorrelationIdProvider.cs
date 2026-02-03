using OrderNotification.Shared.Correlation;

namespace OrderNotification.Application.Abstractions;

/// <summary>
/// Provides the CorrelationId for the current processing context.
/// The inbound message listener is responsible for setting the CorrelationContext from the payload.
/// </summary>
public interface ICorrelationIdProvider
{
    CorrelationId GetCorrelationId();
}
