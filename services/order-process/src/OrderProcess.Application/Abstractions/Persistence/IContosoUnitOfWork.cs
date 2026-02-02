using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Persistence.Abstractions.Repositories.Command;
using OrderProcess.Persistence.Abstractions.Repositories.Query;

namespace OrderProcess.Application.Abstractions.Persistence;

/// <summary>
/// Unit of Work abstraction for the Contoso OLTP database.
///
/// - The application layer depends only on abstractions.
/// - Implementations live in the persistence project (EF Core).
/// </summary>
public interface IContosoUnitOfWork
{
    // CQRS: query + command repositories (Interface Segregation)
    ICustomerQueryRepository CustomerQueries { get; }
    ICustomerCommandRepository CustomerCommands { get; }

    IProductQueryRepository ProductQueries { get; }
    IProductCommandRepository ProductCommands { get; }

    IOrderQueryRepository OrderQueries { get; }
    IOrderCommandRepository OrderCommands { get; }

    IOrderItemQueryRepository OrderItemQueries { get; }
    IOrderItemCommandRepository OrderItemCommands { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current unit-of-work transaction, if any.
    /// This is exposed for critical OLTP workflows where consistency is paramount.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
