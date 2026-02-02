namespace OrderProcess.Domain.Entities.Base;

/// <summary>
/// Base entity for Contoso OLTP tables.
///
/// Provides common columns for CRUD, auditing and soft delete.
/// </summary>
public abstract class EntityBase
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// UTC timestamp for when the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp for when the record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Soft-delete flag. When true, the record should be treated as deleted.
    /// </summary>
    public bool IsSoftDeleted { get; set; }
}
