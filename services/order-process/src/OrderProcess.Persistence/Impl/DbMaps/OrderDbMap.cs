using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcess.Domain.Entities;

namespace OrderProcess.Persistence.Impl.DbMaps;

internal sealed class OrderDbMap : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Order");
        b.ConfigureBaseEntity();

        b.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.CorrelationId).IsUnique();

        b.HasOne(x => x.Customer)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}