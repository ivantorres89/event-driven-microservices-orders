using System.Linq.Expressions;
using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories;

/// <summary>
/// Minimal CRUD contract for a single table.
///
/// Designed for EF Core (LINQ to Entities) usage.
/// </summary>
public interface IBaseRepository<TEntity> where TEntity : EntityBase
{
    /// <summary>
    /// Find by primary key (EF FindAsync).
    /// </summary>
    Task<TEntity?> FindAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find by LINQ predicate (translated to SQL).
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count rows in the table.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Count rows in the table with a LINQ predicate (translated to SQL).
    /// </summary>
    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add an entity (and its children, if any) to the DbContext.
    /// </summary>
    void Add(TEntity entity);

    /// <summary>
    /// Update an entity in the DbContext.
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Soft-delete (sets IsSoftDeleted=true).
    /// </summary>
    void Delete(TEntity entity);

    /// <summary>
    /// Physical delete from the database.
    /// </summary>
    void HardDelete(TEntity entity);
}
