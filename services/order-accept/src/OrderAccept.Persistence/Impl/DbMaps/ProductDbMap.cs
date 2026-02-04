using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderAccept.Domain.Entities;

namespace OrderAccept.Persistence.Impl.DbMaps;

internal sealed class ProductDbMap : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Product");
        b.ConfigureBaseEntity();

        b.Property(x => x.ExternalProductId).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.ExternalProductId).IsUnique();

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}