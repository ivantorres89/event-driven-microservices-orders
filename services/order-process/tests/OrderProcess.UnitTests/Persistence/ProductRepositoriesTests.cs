using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcess.Domain.Entities;
using OrderProcess.Persistence.Impl.Repositories.Command;
using OrderProcess.Persistence.Impl.Repositories.Query;
using OrderProcess.UnitTests.Helpers;

namespace OrderProcess.UnitTests.Persistence;

public sealed class ProductRepositoriesTests
{
    [Fact]
    public async Task GetByExternalIdAsync_WhenProductExists_ReturnsProduct()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        db.Products.Add(new Product
        {
            ExternalProductId = "prod-123",
            Name = "Contoso Priority Support — Monthly",
            Category = "Collaboration",
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 19.99m,
            IsActive = true
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var queryLogger = Mock.Of<ILogger<ProductQueryRepository>>();
        var repo = new ProductQueryRepository(db, queryLogger);

        // Act
        var result = await repo.GetByExternalIdAsync("prod-123");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Contoso Priority Support — Monthly");
    }

    [Fact]
    public async Task GetByExternalIdAsync_WhenProductDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var queryLogger = Mock.Of<ILogger<ProductQueryRepository>>();
        var repo = new ProductQueryRepository(db, queryLogger);

        // Act
        var result = await repo.GetByExternalIdAsync("missing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Add_WhenValid_QueuesInsertAndPersistsOnSaveChanges()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<ProductCommandRepository>>();
        var repo = new ProductCommandRepository(db, logger);

        var product = new Product
        {
            ExternalProductId = "prod-add",
            Name = "Contoso Cash Flow Dashboard — Monthly",
            Category = "Reporting",
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 15m,
            IsActive = true
        };

        // Act
        repo.Add(product);
        await db.SaveChangesAsync();

        // Assert
        var persisted = await db.Products.SingleAsync(p => p.ExternalProductId == "prod-add");
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByExternalIdAsync_WhenDbContextDisposed_Throws()
    {
        // Arrange
        var db = EfTestDb.Create();
        var queryLogger = Mock.Of<ILogger<ProductQueryRepository>>();
        var repo = new ProductQueryRepository(db, queryLogger);
        await db.DisposeAsync();

        // Act
        var act = async () => await repo.GetByExternalIdAsync("prod-123");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
