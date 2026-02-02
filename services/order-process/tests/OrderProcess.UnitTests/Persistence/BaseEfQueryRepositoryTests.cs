using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Impl.Repositories;
using OrderProcess.Persistence.Impl.Repositories.Base;
using OrderProcess.UnitTests.Helpers;

namespace OrderProcess.UnitTests.Persistence;

public sealed class BaseEfQueryRepositoryTests
{
    private sealed class TestProductQueryRepository : BaseEfQueryRepository<Product>
    {
        public TestProductQueryRepository(Persistence.Impl.ContosoDbContext db, ILogger<TestProductQueryRepository> logger)
            : base(db, logger)
        {
        }
    }

    [Fact]
    public async Task FindAsync_WhenEntityExists_ReturnsEntity()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using (var seedDb = EfTestDb.Create(dbName))
        {
            seedDb.Products.Add(new Product
            {
                ExternalProductId = "prod-1",
                Name = "Contoso Easy Invoice — Starter (Monthly)",
                Category = "Billing",
                BillingPeriod = "Monthly",
                IsSubscription = true,
                Price = 9.99m,
                IsActive = true
            });
            await seedDb.SaveChangesAsync();
        }

        await using var db = EfTestDb.Create(dbName);
        var logger = Mock.Of<ILogger<TestProductQueryRepository>>();
        var repo = new TestProductQueryRepository(db, logger);

        var id = await db.Products.Select(p => p.Id).SingleAsync();
        db.ChangeTracker.Clear();

        // Act
        var result = await repo.FindAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.ExternalProductId.Should().Be("prod-1");
        db.ChangeTracker.Entries<Product>().Should().BeEmpty();
    }

    [Fact]
    public async Task FindAsync_WhenEntityDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<TestProductQueryRepository>>();
        var repo = new TestProductQueryRepository(db, logger);

        // Act
        var result = await repo.FindAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WhenMatches_ReturnsEntity()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        db.Products.Add(new Product
        {
            ExternalProductId = "prod-2",
            Name = "Contoso Tax Calendar — Monthly",
            Category = "Tax",
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 5m,
            IsActive = true
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var logger = Mock.Of<ILogger<TestProductQueryRepository>>();
        var repo = new TestProductQueryRepository(db, logger);

        // Act
        var result = await repo.FirstOrDefaultAsync(p => p.ExternalProductId == "prod-2");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Contoso Tax Calendar — Monthly");
    }

    [Fact]
    public async Task CountAsync_WhenCalled_ReturnsEntityCount()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        db.Products.AddRange(
            new Product
            {
                ExternalProductId = "prod-a",
                Name = "A",
                Category = "Billing",
                BillingPeriod = "Monthly",
                IsSubscription = true,
                Price = 1m,
                IsActive = true
            },
            new Product
            {
                ExternalProductId = "prod-b",
                Name = "B",
                Category = "Reporting",
                BillingPeriod = "Annual",
                IsSubscription = true,
                Price = 2m,
                IsActive = true
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var logger = Mock.Of<ILogger<TestProductQueryRepository>>();
        var repo = new TestProductQueryRepository(db, logger);

        // Act
        var count = await repo.CountAsync();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task CountAsync_WhenFiltered_ReturnsFilteredCount()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        db.Products.AddRange(
            new Product
            {
                ExternalProductId = "prod-a",
                Name = "A",
                Category = "Billing",
                BillingPeriod = "Monthly",
                IsSubscription = true,
                Price = 1m,
                IsActive = true
            },
            new Product
            {
                ExternalProductId = "prod-b",
                Name = "B",
                Category = "Billing",
                BillingPeriod = "Annual",
                IsSubscription = true,
                Price = 2m,
                IsActive = false
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var logger = Mock.Of<ILogger<TestProductQueryRepository>>();
        var repo = new TestProductQueryRepository(db, logger);

        // Act
        var count = await repo.CountAsync(p => p.IsActive);

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task FindAsync_WhenDbContextDisposed_Throws()
    {
        // Arrange
        var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<TestProductQueryRepository>>();
        var repo = new TestProductQueryRepository(db, logger);
        await db.DisposeAsync();

        // Act
        var act = async () => await repo.FindAsync(1);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
