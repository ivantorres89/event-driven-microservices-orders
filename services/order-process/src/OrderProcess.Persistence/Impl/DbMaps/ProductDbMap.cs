using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcess.Domain.Entities;

namespace OrderProcess.Persistence.Impl.DbMaps;

internal sealed class ProductDbMap : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Product");
        b.ConfigureBaseEntity();

        b.Property(x => x.ExternalProductId).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.ExternalProductId).IsUnique();

        b.Property(x => x.Name).HasMaxLength(200).IsRequired();

        b.Property(x => x.Category).HasMaxLength(100).IsRequired();
        b.Property(x => x.BillingPeriod).HasMaxLength(16).IsRequired();

        b.Property(x => x.Vendor).HasMaxLength(64).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(2048).IsRequired();
        b.Property(x => x.Discount).IsRequired();
    }
}