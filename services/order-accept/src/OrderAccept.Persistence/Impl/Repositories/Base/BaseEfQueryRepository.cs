using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderAccept.Persistence.Abstractions.Repositories.Base;

namespace OrderAccept.Persistence.Impl.Repositories.Base;

/// <summary>
/// Base EF Core implementation for CQRS query repositories.
///
/// - Uses AsNoTracking() by default.
/// - Logs errors (but avoids noisy per-query logging).
/// </summary>
public abstract class BaseEfQueryRepository<TEntity> : IQueryRepository<TEntity>
    where TEntity : class
{
    protected readonly ContosoDbContext Db;
    protected readonly ILogger Logger;

    protected BaseEfQueryRepository(ContosoDbContext db, ILogger logger)
    {
        Db = db;
        Logger = logger;
    }

    /// <summary>
    /// Base queryable for this entity.
    /// Derived types can override to apply Includes.
    /// </summary>
    protected virtual IQueryable<TEntity> Queryable(bool asNoTracking)
        => asNoTracking ? Db.Set<TEntity>().AsNoTracking() : Db.Set<TEntity>();

    public virtual async Task<TEntity?> FindAsync(
        long id,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generic lookup by shadow-property access to "Id".
            return await Queryable(asNoTracking).FirstOrDefaultAsync(
                e => EF.Property<long>(e, "Id") == id,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to query {Entity} by Id={Id}", typeof(TEntity).Name, id);
            throw;
        }
    }

    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Queryable(asNoTracking).FirstOrDefaultAsync(predicate, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to query {Entity} with predicate", typeof(TEntity).Name);
            throw;
        }
    }

    public virtual async Task<int> CountAsync(
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Queryable(asNoTracking).CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to count {Entity}", typeof(TEntity).Name);
            throw;
        }
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Queryable(asNoTracking).CountAsync(predicate, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to count {Entity} with predicate", typeof(TEntity).Name);
            throw;
        }
    }
}
