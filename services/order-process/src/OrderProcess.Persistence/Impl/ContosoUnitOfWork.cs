using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Persistence.Abstractions.Repositories.Command;
using OrderProcess.Persistence.Abstractions.Repositories.Query;
using OrderProcess.Persistence.Impl.Transactions;

namespace OrderProcess.Persistence.Impl;

public sealed class ContosoUnitOfWork : IContosoUnitOfWork
{
    private readonly ContosoDbContext _db;
    private readonly IContosoTransactionFactory _transactionFactory;
    private readonly ILogger<ContosoUnitOfWork> _logger;

    private IDbContextTransaction? _tx;

    public ContosoUnitOfWork(
        ContosoDbContext db,
        IContosoTransactionFactory transactionFactory,
        ICustomerQueryRepository customerQueries,
        ICustomerCommandRepository customerCommands,
        IProductQueryRepository productQueries,
        IProductCommandRepository productCommands,
        IOrderQueryRepository orderQueries,
        IOrderCommandRepository orderCommands,
        IOrderItemQueryRepository orderItemQueries,
        IOrderItemCommandRepository orderItemCommands,
        ILogger<ContosoUnitOfWork> logger)
    {
        _db = db;
        _transactionFactory = transactionFactory;
        _logger = logger;

        CustomerQueries = customerQueries;
        CustomerCommands = customerCommands;

        ProductQueries = productQueries;
        ProductCommands = productCommands;

        OrderQueries = orderQueries;
        OrderCommands = orderCommands;

        OrderItemQueries = orderItemQueries;
        OrderItemCommands = orderItemCommands;
    }

    public ICustomerQueryRepository CustomerQueries { get; }
    public ICustomerCommandRepository CustomerCommands { get; }

    public IProductQueryRepository ProductQueries { get; }
    public IProductCommandRepository ProductCommands { get; }

    public IOrderQueryRepository OrderQueries { get; }
    public IOrderCommandRepository OrderCommands { get; }

    public IOrderItemQueryRepository OrderItemQueries { get; }
    public IOrderItemCommandRepository OrderItemCommands { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTransactionAsync(cancellationToken);

        try
        {
            var rows = await _db.SaveChangesAsync(cancellationToken);
            await _tx!.CommitAsync(cancellationToken);
            _logger.LogInformation("Committed Contoso OLTP transaction. Rows={Rows}", rows);

            await _tx.DisposeAsync();
            _tx = null;
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit Contoso OLTP transaction. Rolling back.");
            await RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_tx is null)
            return;

        try
        {
            await _tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("Rolled back Contoso OLTP transaction");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback Contoso OLTP transaction");
            throw;
        }
        finally
        {
            await _tx.DisposeAsync();
            _tx = null;
        }
    }

    private async Task EnsureTransactionAsync(CancellationToken cancellationToken)
    {
        if (_tx is not null)
            return;

        _tx = await _transactionFactory.BeginTransactionAsync(_db, cancellationToken);
        _logger.LogInformation("Began Contoso OLTP transaction");
    }
}
