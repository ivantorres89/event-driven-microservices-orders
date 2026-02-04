using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;

namespace OrderAccept.Application.Handlers;

public sealed class SoftDeleteOrderHandler : ISoftDeleteOrderHandler
{
    private readonly IContosoUnitOfWork _uow;

    public SoftDeleteOrderHandler(IContosoUnitOfWork uow) => _uow = uow;

    public async Task<bool> HandleAsync(
        long orderId,
        string externalCustomerId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(externalCustomerId))
            return false;

        // Resolve internal customer key.
        var customer = await _uow.CustomerQueries.GetByExternalIdAsync(externalCustomerId, asNoTracking: true, cancellationToken);
        if (customer is null)
            return false;

        // We need tracking for the write; this is a write workflow, not a read.
        var order = await _uow.OrderQueries.FindAsync(orderId, asNoTracking: false, cancellationToken);
        if (order is null || order.CustomerId != customer.Id)
            return false;

        order.IsSoftDeleted = true;

        // If the entity is tracked, Update is technically optional, but we keep it explicit.
        _uow.OrderCommands.Update(order);

        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
