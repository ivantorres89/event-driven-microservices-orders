using Microsoft.EntityFrameworkCore.Storage;

namespace OrderAccept.Persistence.Impl.Transactions;

public interface IContosoTransactionFactory
{
    Task<IDbContextTransaction> BeginTransactionAsync(
        ContosoDbContext db,
        CancellationToken cancellationToken = default);
}
