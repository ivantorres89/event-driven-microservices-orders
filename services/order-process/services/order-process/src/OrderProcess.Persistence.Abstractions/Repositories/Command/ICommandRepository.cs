namespace OrderProcess.Persistence.Abstractions.Repositories.Command;

/// <summary>
/// CQRS write-side repository abstraction.
///
/// NOTE:
/// - Keep this interface strictly write-only (Interface Segregation).
/// - Persistence is deferred until the UnitOfWork commits (SaveChangesAsync).
/// </summary>
public interface ICommandRepository<TEntity>
    where TEntity : class
{
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);

    void Update(TEntity entity);
    void UpdateRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Removes the entity from the persistence store.
    /// If soft-delete is implemented at the entity level, this should mark the entity as deleted.
    /// </summary>
    void Delete(TEntity entity);
    void DeleteRange(IEnumerable<TEntity> entities);
}