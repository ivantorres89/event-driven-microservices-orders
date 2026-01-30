namespace OrderProcess.Shared.Resilience;

/// <summary>
/// Signals that a required infrastructure dependency (e.g., Redis or RabbitMQ)
/// is temporarily unavailable. The API maps this to HTTP 503.
/// </summary>
public sealed class DependencyUnavailableException : Exception
{
    public DependencyUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
