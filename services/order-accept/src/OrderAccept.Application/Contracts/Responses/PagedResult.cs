namespace OrderAccept.Application.Contracts.Responses;

/// <summary>
/// Simple pagination envelope used by read endpoints.
///
/// JSON serialization uses camelCase.
/// </summary>
public sealed record PagedResult<T>(
    int Offset,
    int Size,
    int Total,
    IReadOnlyCollection<T> Items
);
