using OrderAccept.Shared.Correlation;

namespace OrderAccept.Application.Abstractions
{
    /// <summary>
    /// Defines a provider for managing correlation identifiers across requests.
    /// </summary>
    public interface ICorrelationIdProvider
    {
        /// <summary>
        /// Generates a new correlation identifier.
        /// </summary>
        /// <returns>A newly generated correlation identifier.</returns>
        CorrelationId GenerateCorrelationId();
    }
}