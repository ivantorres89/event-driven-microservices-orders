using OrderNotification.Shared.Correlation;

namespace OrderNotification.Application.Abstractions;

/// <summary>
/// Maps a business CorrelationId to a logical user identifier.
/// This is used by order-notification to route real-time updates to the correct SignalR user.
/// </summary>
public interface IOrderCorrelationRegistry
{
    Task RegisterAsync(
        CorrelationId correlationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<string?> ResolveUserIdAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default);
}
