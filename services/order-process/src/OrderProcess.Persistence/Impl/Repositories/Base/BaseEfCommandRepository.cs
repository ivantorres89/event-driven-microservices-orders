using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Base;

/// <summary>
/// Base EF Core implementation for CQRS command repositories.
///
/// - Only queues EF Core changes (no SaveChanges here).
/// - Logs information about critical write operations, and logs errors if they fail.
/// </summary>
public abstract class BaseEfCommandRepository<TEntity> : ICommandRepository<TEntity>
    where TEntity : class
{
    protected readonly ContosoDbContext Db;
    protected readonly ILogger Logger;

    protected BaseEfCommandRepository(ContosoDbContext db, ILogger logger)
    {
        Db = db;
        Logger = logger;
    }

    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    public virtual void Add(TEntity entity)
    {
        try
        {
            Set.Add(entity);
            Logger.LogInformation("Queued INSERT for {Entity}", typeof(TEntity).Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to queue INSERT for {Entity}", typeof(TEntity).Name);
            throw;
        }
    }

    public virtual void AddRange(IEnumerable<TEntity> entities)
    {
        try
        {
            Set.AddRange(entities);
            Logger.LogInformation("Queued INSERT range for {Entity}", typeof(TEntity).Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to queue INSERT range for {Entity}", typeof(TEntity).Name);
            throw;
        }
    }

    public virtual void Update(TEntity entity)
    {
        try
        {
            Set.Update(entity);
            Logger.LogInformation("Queued UPDATE for {Entity}", typeof(TEntity).Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to queue UPDATE for {Entity}", typeof(TEntity).Name);
            throw;
        }
    }

    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
        try
        {
            Set.UpdateRange(entities);
            Logger.LogInformation("Queued UPDATE range for {Entity}", typeof(TEntity).Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to queue UPDATE range for {Entity}", typeof(TEntity).Name);
            throw;
        }
    }

    public virtual void Delete(TEntity entity)
    {
        try
        {
            Set.Remove(entity);
            Logger.LogInformation("Queued DELETE for {Entity}", typeof(TEntity).Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to queue DELETE for {Entity}", typeof(TEntity).Name);
            throw;
        }
    }

    public virtual void DeleteRange(IEnumerable<TEntity> entities)
    {
        try
        {
            Set.RemoveRange(entities);
            Logger.LogInformation("Queued DELETE range for {Entity}", typeof(TEntity).Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to queue DELETE range for {Entity}", typeof(TEntity).Name);
            throw;
        }
    }
}
