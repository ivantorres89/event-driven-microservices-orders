using Microsoft.EntityFrameworkCore.Storage;

namespace OrderProcess.Persistence.Impl.Transactions;

public interface IContosoTransactionFactory
{
    Task<IDbContextTransaction> BeginTransactionAsync(
        ContosoDbContext db,
        CancellationToken cancellationToken = default);
}
