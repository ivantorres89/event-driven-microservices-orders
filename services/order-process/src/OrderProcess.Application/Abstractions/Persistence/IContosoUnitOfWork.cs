using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Application.Abstractions.Persistence;

/// <summary>
/// Unit of Work abstraction for the Contoso OLTP database.
///
/// - The application layer depends only on abstractions.
/// - Implementations live in the persistence project (EF Core).
/// </summary>
public interface IContosoUnitOfWork
{
    ICustomerRepository Customers { get; }
    IOrderRepository Orders { get; }
    IProductRepository Products { get; }
    IOrderItemRepository OrderItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
