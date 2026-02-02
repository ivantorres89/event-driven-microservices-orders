using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

internal abstract class BaseEfRepository<TEntity> : IBaseRepository<TEntity> where TEntity : EntityBase
{
    protected BaseEfRepository(ContosoDbContext db) => Db = db;

    protected ContosoDbContext Db { get; }

    public virtual Task<TEntity?> FindAsync(long id, CancellationToken cancellationToken = default)
        => Db.Set<TEntity>().FindAsync(new object?[] { id }, cancellationToken).AsTask();

    public virtual Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Db.Set<TEntity>().FirstOrDefaultAsync(predicate, cancellationToken);

    public virtual Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Db.Set<TEntity>().CountAsync(cancellationToken);

    public virtual Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Db.Set<TEntity>().CountAsync(predicate, cancellationToken);

    public virtual void Add(TEntity entity) => Db.Set<TEntity>().Add(entity);

    public virtual void Update(TEntity entity) => Db.Set<TEntity>().Update(entity);

    public virtual void Delete(TEntity entity)
    {
        entity.IsSoftDeleted = true;
        Db.Set<TEntity>().Update(entity);
    }

    public virtual void HardDelete(TEntity entity) => Db.Set<TEntity>().Remove(entity);
}
