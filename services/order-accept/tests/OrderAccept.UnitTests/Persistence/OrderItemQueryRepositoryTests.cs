using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Impl.Repositories.Query;

namespace OrderAccept.UnitTests.Persistence;

public sealed class OrderItemQueryRepositoryTests
{
    [Fact]
    public async Task FindAsync_IgnoresSoftDeletedItems()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = PersistenceDbContextFactory.CreateDbContext(databaseName);
        var logger = new Mock<ILogger<OrderItemQueryRepository>>();

        var product = new Product { Id = 10, ExternalProductId = "p-10" };
        db.Products.Add(product);

        db.OrderItems.AddRange(
            new OrderItem { Id = 1, Product = product, ProductId = product.Id, IsSoftDeleted = false },
            new OrderItem { Id = 2, Product = product, ProductId = product.Id, IsSoftDeleted = true });
        await db.SaveChangesAsync();

        using var queryDb = PersistenceDbContextFactory.CreateDbContext(databaseName);
        var repo = new OrderItemQueryRepository(queryDb, logger.Object);

        var result = await repo.FindAsync(2);

        result.Should().BeNull();
    }
}
