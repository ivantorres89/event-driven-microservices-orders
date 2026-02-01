using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Impl;

public sealed class ContosoDbContext : DbContext
{
    public ContosoDbContext(DbContextOptions<ContosoDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- Customer ---
        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("Customer");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();

            b.Property(x => x.ExternalCustomerId).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.ExternalCustomerId).IsUnique();

            b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            b.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
            b.Property(x => x.NationalId).HasMaxLength(32).IsRequired();

            b.Property(x => x.AddressLine1).HasMaxLength(200);
            b.Property(x => x.City).HasMaxLength(100);
            b.Property(x => x.PostalCode).HasMaxLength(20);
            b.Property(x => x.CountryCode).HasMaxLength(2);

            var created = b.Property(x => x.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAdd();
            created.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            created.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            var updated = b.Property(x => x.UpdatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAddOrUpdate();
            updated.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            updated.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        });

        // --- Product (Item) ---
        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Product");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();

            b.Property(x => x.ExternalProductId).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.ExternalProductId).IsUnique();

            b.Property(x => x.Name).HasMaxLength(200).IsRequired();

            b.Property(x => x.Category).HasMaxLength(100).IsRequired();
            b.Property(x => x.BillingPeriod).HasMaxLength(16).IsRequired();
            b.Property(x => x.IsSubscription).IsRequired();
            b.Property(x => x.Price).HasColumnType("decimal(10,2)").IsRequired();
            b.Property(x => x.IsActive).IsRequired();

            var created = b.Property(x => x.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAdd();
            created.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            created.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            var updated = b.Property(x => x.UpdatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAddOrUpdate();
            updated.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            updated.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        });

        // --- Order ---
        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Order");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();

            b.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.CorrelationId).IsUnique();

            var created = b.Property(x => x.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAdd();
            created.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            created.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            var updated = b.Property(x => x.UpdatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAddOrUpdate();
            updated.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            updated.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            b.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- OrderItem ---
        modelBuilder.Entity<OrderItem>(b =>
        {
            b.ToTable("OrderItem");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();

            b.Property(x => x.Quantity).IsRequired();

            var created = b.Property(x => x.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAdd();
            created.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            created.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            var updated = b.Property(x => x.UpdatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAddOrUpdate();
            updated.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            updated.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            b.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Product)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(modelBuilder);
    }
}
