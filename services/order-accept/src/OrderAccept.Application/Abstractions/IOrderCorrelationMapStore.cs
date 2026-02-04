using OrderAccept.Shared.Correlation;

namespace OrderAccept.Application.Abstractions;

/// <summary>
/// Stores a short-lived mapping between CorrelationId and the authenticated user identifier.
/// This is used by the real-time notification service to resolve who should be notified
/// when an asynchronous workflow completes, without placing user identifiers on messages.
///
/// Redis is expected to be TTL-based and is NOT a system of record.
/// </summary>
public interface IOrderCorrelationMapStore
{
    /// <summary>
    /// Sets the CorrelationId -> UserId mapping with a TTL.
    /// </summary>
    Task SetUserIdAsync(
        CorrelationId correlationId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the mapped UserId for the specified CorrelationId, or null if not present/expired.
    /// </summary>
    Task<string?> GetUserIdAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the TTL of the mapping key (best-effort).
    /// </summary>
    Task RefreshTtlAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the CorrelationId -> UserId mapping (best-effort).
    /// </summary>
    Task RemoveAsync(
        CorrelationId correlationId,
        CancellationToken cancellationToken = default);
}
