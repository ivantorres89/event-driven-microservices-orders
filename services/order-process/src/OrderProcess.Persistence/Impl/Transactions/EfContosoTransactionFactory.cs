using Microsoft.EntityFrameworkCore.Storage;

namespace OrderProcess.Persistence.Impl.Transactions;

public sealed class EfContosoTransactionFactory : IContosoTransactionFactory
{
    public Task<IDbContextTransaction> BeginTransactionAsync(
        ContosoDbContext db,
        CancellationToken cancellationToken = default)
        => db.Database.BeginTransactionAsync(cancellationToken);
}
