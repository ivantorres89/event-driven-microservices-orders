using System.Linq.Expressions;

namespace OrderProcess.Persistence.Abstractions.Repositories.Base;

/// <summary>
/// CQRS read-side repository abstraction.
///
/// NOTE:
/// - Keep this interface strictly read-only (Interface Segregation).
/// - LINQ predicates are expressed as Expression trees so EF can translate them.
/// </summary>
public interface IQueryRepository<TEntity>
    where TEntity : class
{
    Task<TEntity?> FindAsync(long id, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);
}
