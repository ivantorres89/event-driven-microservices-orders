using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderAccept.Domain.Entities;

namespace OrderAccept.Persistence.Impl.DbMaps;

internal sealed class CustomerDbMap : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customer", tb => tb.HasTrigger("trg_Customer_SetUpdatedAt"));
        b.ConfigureBaseEntity();

        b.Property(x => x.ExternalCustomerId).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.ExternalCustomerId).IsUnique();

        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
        b.Property(x => x.NationalId).HasMaxLength(32).IsRequired();

        b.Property(x => x.AddressLine1).HasMaxLength(200).IsRequired();
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();

        b.Property(x => x.DateOfBirth).HasColumnType("date");
    }
}
