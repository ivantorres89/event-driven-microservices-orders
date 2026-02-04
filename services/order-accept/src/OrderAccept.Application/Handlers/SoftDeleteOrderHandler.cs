using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;

namespace OrderAccept.Application.Handlers;

public sealed class SoftDeleteOrderHandler : ISoftDeleteOrderHandler
{
    private readonly IContosoUnitOfWork _uow;

    public SoftDeleteOrderHandler(IContosoUnitOfWork uow) => _uow = uow;

    public async Task<SoftDeleteOrderOutcome> HandleAsync(
        long orderId,
        string externalCustomerId,
        CancellationToken cancellationToken = default)
    {
        // Inputs are validated at the API boundary; remain defensive.
        if (orderId <= 0 || string.IsNullOrWhiteSpace(externalCustomerId))
            return SoftDeleteOrderOutcome.NotFound;

        // Resolve internal customer key.
        var customer = await _uow.CustomerQueries.GetByExternalIdAsync(externalCustomerId, asNoTracking: true, cancellationToken);
        if (customer is null)
            return SoftDeleteOrderOutcome.NotFound;

        // We need tracking for the write.
        var order = await _uow.OrderQueries.FindAsync(orderId, asNoTracking: false, cancellationToken);
        if (order is null)
            return SoftDeleteOrderOutcome.NotFound;

        if (order.CustomerId != customer.Id)
            return SoftDeleteOrderOutcome.Forbidden;

        order.IsSoftDeleted = true;
        _uow.OrderCommands.Update(order);

        await _uow.SaveChangesAsync(cancellationToken);
        return SoftDeleteOrderOutcome.Deleted;
    }
}
