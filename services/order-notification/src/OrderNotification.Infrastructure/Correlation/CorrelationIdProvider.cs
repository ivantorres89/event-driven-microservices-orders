using OrderNotification.Application.Abstractions;
using OrderNotification.Shared.Correlation;

namespace OrderNotification.Infrastructure.Correlation
{
    /// <summary>
    /// Default implementation of <see cref="ICorrelationIdProvider"/> that generates new correlation identifiers.
    /// </summary>
    public sealed class CorrelationIdProvider : ICorrelationIdProvider
    {
        public CorrelationId GetCorrelationId() => CorrelationId.New();
    }
}