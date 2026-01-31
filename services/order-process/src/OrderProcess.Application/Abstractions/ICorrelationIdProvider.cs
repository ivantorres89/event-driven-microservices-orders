using OrderProcess.Shared.Correlation;

namespace OrderProcess.Application.Abstractions;

/// <summary>
/// Provides access to the current workflow CorrelationId.
/// In this worker, the CorrelationId is expected to be extracted from the inbound message payload and
/// stored in <see cref="CorrelationContext"/> for the duration of message processing.
/// </summary>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// Gets the current CorrelationId for the message being processed.
    /// </summary>
    CorrelationId GetCorrelationId();
}
