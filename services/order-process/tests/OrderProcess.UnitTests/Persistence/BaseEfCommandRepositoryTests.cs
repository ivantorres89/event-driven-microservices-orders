using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcess.Domain.Entities;
using OrderProcess.Persistence.Impl;
using OrderProcess.Persistence.Impl.Repositories.Base;
using OrderProcess.UnitTests.Helpers;

namespace OrderProcess.UnitTests.Persistence;

public sealed class BaseEfCommandRepositoryTests
{
    public sealed class TestProductCommandRepository : BaseEfCommandRepository<Product>
    {
        public TestProductCommandRepository(ContosoDbContext db, ILogger<TestProductCommandRepository> logger)
            : base(db, logger)
        {
        }
    }

    [Fact]
    public async Task Add_WhenValid_QueuesInsertAndPersistsOnSaveChanges()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<TestProductCommandRepository>>();
        var repo = new TestProductCommandRepository(db, logger);

        var product = new Product
        {
            ExternalProductId = "prod-1",
            Name = "Contoso Sales Dashboard — Annual",
            Category = "Reporting",
            BillingPeriod = "Annual",
            IsSubscription = true,
            Price = 49m,
            IsActive = true
        };

        // Act
        repo.Add(product);
        await db.SaveChangesAsync();

        // Assert
        var persisted = await db.Products.SingleAsync(p => p.ExternalProductId == "prod-1");
        persisted.Name.Should().Be("Contoso Sales Dashboard — Annual");
    }

    [Fact]
    public async Task Update_WhenValid_QueuesUpdateAndPersistsOnSaveChanges()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using (var seedDb = EfTestDb.Create(dbName))
        {
            seedDb.Products.Add(new Product
            {
                ExternalProductId = "prod-2",
                Name = "Old",
                Category = "Billing",
                BillingPeriod = "Monthly",
                IsSubscription = true,
                Price = 10m,
                IsActive = true
            });
            await seedDb.SaveChangesAsync();
        }

        await using var db = EfTestDb.Create(dbName);
        var logger = Mock.Of<ILogger<TestProductCommandRepository>>();
        var repo = new TestProductCommandRepository(db, logger);

        var product = await db.Products.SingleAsync(p => p.ExternalProductId == "prod-2");
        product.Name = "New";

        // Act
        repo.Update(product);
        await db.SaveChangesAsync();

        // Assert
        var persisted = await db.Products.SingleAsync(p => p.ExternalProductId == "prod-2");
        persisted.Name.Should().Be("New");
    }

    [Fact]
    public async Task Delete_WhenValid_QueuesDeleteAndRemovesOnSaveChanges()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        db.Products.Add(new Product
        {
            ExternalProductId = "prod-3",
            Name = "To delete",
            Category = "Billing",
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 1m,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var logger = Mock.Of<ILogger<TestProductCommandRepository>>();
        var repo = new TestProductCommandRepository(db, logger);
        var entity = await db.Products.SingleAsync(p => p.ExternalProductId == "prod-3");

        // Act
        repo.Delete(entity);
        await db.SaveChangesAsync();

        // Assert
        var exists = await db.Products.AnyAsync(p => p.ExternalProductId == "prod-3");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Add_WhenDbContextDisposed_Throws()
    {
        // Arrange
        var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<TestProductCommandRepository>>();
        var repo = new TestProductCommandRepository(db, logger);
        await db.DisposeAsync();

        // Act
        var act = () => repo.Add(new Product { ExternalProductId = "prod-x", Name = "x", Category = "Billing", BillingPeriod = "Monthly", IsSubscription = true, Price = 1m, IsActive = true });

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}
