namespace OrderAccept.Application.Abstractions;

public interface ISoftDeleteOrderHandler
{
    Task<bool> HandleAsync(
        long orderId,
        string externalCustomerId,
        CancellationToken cancellationToken = default);
}
