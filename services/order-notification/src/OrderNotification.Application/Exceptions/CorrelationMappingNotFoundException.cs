namespace OrderNotification.Application.Exceptions;

/// <summary>
/// Raised when the correlationId -&gt; userId mapping cannot be found in the transient store (Redis).
///
/// This should be treated as a retryable condition by the message listener, because the mapping
/// may appear shortly after (e.g., eventual consistency) or have been refreshed by other services.
/// </summary>
public sealed class CorrelationMappingNotFoundException : Exception
{
    public CorrelationMappingNotFoundException(string correlationId)
        : base($"Correlation mapping not found for CorrelationId={correlationId}")
    {
    }
}
