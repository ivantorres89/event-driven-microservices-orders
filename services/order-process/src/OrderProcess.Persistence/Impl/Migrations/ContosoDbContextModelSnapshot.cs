using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderProcess.Persistence.Impl;

#nullable disable

namespace OrderProcess.Persistence.Impl.Migrations;

[DbContext(typeof(ContosoDbContext))]
partial class ContosoDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.0");

        modelBuilder.Entity("OrderProcess.Persistence.Abstractions.Entities.Customer", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasAnnotation("SqlServer:Identity", "1, 1");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<bool>("IsSoftDeleted")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<bool>("IsSoftDeleted")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<string>("AddressLine1")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            b.Property<string>("City")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<string>("CountryCode")
                .IsRequired()
                .HasMaxLength(2)
                .HasColumnType("nvarchar(2)");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<bool>("IsSoftDeleted")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<DateOnly?>("DateOfBirth")
                .HasColumnType("date");

            b.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("nvarchar(256)");

            b.Property<string>("ExternalCustomerId")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("nvarchar(64)");

            b.Property<string>("FirstName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<string>("LastName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<string>("NationalId")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("nvarchar(32)");

            b.Property<string>("PhoneNumber")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("nvarchar(32)");

            b.Property<string>("PostalCode")
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnType("nvarchar(20)");

            b.HasKey("Id");

            b.HasIndex("ExternalCustomerId")
                .IsUnique();

            b.ToTable("Customer", (string)null);
        });

        modelBuilder.Entity("OrderProcess.Persistence.Abstractions.Entities.Product", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasAnnotation("SqlServer:Identity", "1, 1");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<bool>("IsSoftDeleted")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<string>("ExternalProductId")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("nvarchar(64)");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            b.HasKey("Id");

            b.HasIndex("ExternalProductId")
                .IsUnique();

            b.ToTable("Product", (string)null);
        });

        modelBuilder.Entity("OrderProcess.Persistence.Abstractions.Entities.Order", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasAnnotation("SqlServer:Identity", "1, 1");

            b.Property<string>("CorrelationId")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("nvarchar(64)");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<bool>("IsSoftDeleted")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<long>("CustomerId")
                .HasColumnType("bigint");

            b.HasKey("Id");

            b.HasIndex("CorrelationId")
                .IsUnique();

            b.HasIndex("CustomerId");

            b.ToTable("Order", (string)null);
        });

        modelBuilder.Entity("OrderProcess.Persistence.Abstractions.Entities.OrderItem", b =>
        {
            b.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("bigint")
                .HasAnnotation("SqlServer:Identity", "1, 1");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<bool>("IsSoftDeleted")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            b.Property<long>("OrderId")
                .HasColumnType("bigint");

            b.Property<long>("ProductId")
                .HasColumnType("bigint");

            b.Property<int>("Quantity")
                .HasColumnType("int");

            b.HasKey("Id");

            b.HasIndex("OrderId");

            b.HasIndex("ProductId");

            b.ToTable("OrderItem", (string)null);
        });

        modelBuilder.Entity("OrderProcess.Persistence.Abstractions.Entities.Order", b =>
        {
            b.HasOne("OrderProcess.Persistence.Abstractions.Entities.Customer", "Customer")
                .WithMany("Orders")
                .HasForeignKey("CustomerId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            b.Navigation("Customer");
        });

        modelBuilder.Entity("OrderProcess.Persistence.Abstractions.Entities.OrderItem", b =>
        {
            b.HasOne("OrderProcess.Persistence.Abstractions.Entities.Order", "Order")
                .WithMany("Items")
                .HasForeignKey("OrderId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("OrderProcess.Persistence.Abstractions.Entities.Product", "Product")
                .WithMany("OrderItems")
                .HasForeignKey("ProductId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            b.Navigation("Order");
            b.Navigation("Product");
        });
    }
}
