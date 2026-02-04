using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderAccept.Domain.Entities.Base;

namespace OrderAccept.Persistence.Impl.DbMaps;

internal static class DbMapExtensions
{
    public static void ConfigureBaseEntity<TEntity>(this EntityTypeBuilder<TEntity> b)
        where TEntity : EntityBase
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAdd();

        b.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAddOrUpdate();

        // Do not allow updates to CreatedAt once persisted.
        b.Property(x => x.CreatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        // UpdatedAt is controlled by the app (and later can be moved to a DB trigger).
        b.Property(x => x.UpdatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);

        b.Property(x => x.IsSoftDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Default query filter: exclude soft-deleted rows.
        b.HasQueryFilter(x => !x.IsSoftDeleted);
    }
}
