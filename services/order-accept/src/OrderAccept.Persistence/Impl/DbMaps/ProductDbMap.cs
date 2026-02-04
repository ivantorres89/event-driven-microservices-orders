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
        b.Property(x => x.Category).HasMaxLength(100).IsRequired();
        b.Property(x => x.Vendor).HasMaxLength(100).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();

        b.Property(x => x.Discount).IsRequired();
        b.Property(x => x.BillingPeriod).HasMaxLength(32).IsRequired();
        b.Property(x => x.IsSubscription).IsRequired();

        // SQL Server money/decimal friendliness
        b.Property(x => x.Price).HasColumnType("decimal(18,2)").IsRequired();

        b.Property(x => x.IsActive).IsRequired();
    }
}
