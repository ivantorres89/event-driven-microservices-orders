namespace OrderAccept.Application.Abstractions;

public interface ISoftDeleteOrderHandler
{
    Task<SoftDeleteOrderOutcome> HandleAsync(
        long orderId,
        string externalCustomerId,
        CancellationToken cancellationToken = default);
}
