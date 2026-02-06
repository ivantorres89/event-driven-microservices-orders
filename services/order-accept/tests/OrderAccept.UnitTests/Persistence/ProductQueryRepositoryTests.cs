using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Impl.Repositories.Query;

namespace OrderAccept.UnitTests.Persistence;

public sealed class ProductQueryRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveAndNotSoftDeleted()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<ProductQueryRepository>>();
        var repo = new ProductQueryRepository(db, logger.Object);

        db.Products.AddRange(
            new Product { Id = 1, ExternalProductId = "p-1", IsActive = true, IsSoftDeleted = false },
            new Product { Id = 2, ExternalProductId = "p-2", IsActive = false, IsSoftDeleted = false },
            new Product { Id = 3, ExternalProductId = "p-3", IsActive = true, IsSoftDeleted = true });
        await db.SaveChangesAsync();

        var result = await repo.GetAllAsync();

        result.Should().ContainSingle(p => p.ExternalProductId == "p-1");
    }

    [Fact]
    public async Task GetByExternalIdsAsync_WhenEmpty_ReturnsEmpty()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<ProductQueryRepository>>();
        var repo = new ProductQueryRepository(db, logger.Object);

        var result = await repo.GetByExternalIdsAsync(Array.Empty<string>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPageOrderedById()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<ProductQueryRepository>>();
        var repo = new ProductQueryRepository(db, logger.Object);

        db.Products.AddRange(
            new Product { Id = 1, ExternalProductId = "p-1", IsActive = true },
            new Product { Id = 2, ExternalProductId = "p-2", IsActive = true },
            new Product { Id = 3, ExternalProductId = "p-3", IsActive = true });
        await db.SaveChangesAsync();

        var result = await repo.GetPagedAsync(offset: 1, size: 1);

        result.Should().ContainSingle();
        result[0].ExternalProductId.Should().Be("p-2");
    }
}
