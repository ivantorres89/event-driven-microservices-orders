namespace OrderAccept.Application.Contracts.Responses;

/// <summary>
/// Simple pagination envelope used by read endpoints.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int Offset,
    int Size,
    int TotalCount
);
