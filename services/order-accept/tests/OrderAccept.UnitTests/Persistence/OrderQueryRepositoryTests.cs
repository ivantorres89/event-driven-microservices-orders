using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Impl.Repositories.Query;

namespace OrderAccept.UnitTests.Persistence;

public sealed class OrderQueryRepositoryTests
{
    [Fact]
    public async Task GetByCorrelationIdAsync_FiltersSoftDeletedItemsAndIncludesProduct()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = PersistenceDbContextFactory.CreateDbContext(databaseName);
        var logger = new Mock<ILogger<OrderQueryRepository>>();

        var customer = new Customer { Id = 1, ExternalCustomerId = "c-1" };
        var product = new Product { Id = 10, ExternalProductId = "p-10" };
        var order = new Order { Id = 100, CorrelationId = "corr-1", CustomerId = customer.Id, Customer = customer };
        var activeItem = new OrderItem { Id = 1000, Order = order, OrderId = order.Id, Product = product, ProductId = product.Id, IsSoftDeleted = false };
        var deletedItem = new OrderItem { Id = 1001, Order = order, OrderId = order.Id, Product = product, ProductId = product.Id, IsSoftDeleted = true };
        order.Items.Add(activeItem);
        order.Items.Add(deletedItem);

        db.Customers.Add(customer);
        db.Products.Add(product);
        db.Orders.Add(order);
        db.OrderItems.AddRange(activeItem, deletedItem);
        await db.SaveChangesAsync();

        using var queryDb = PersistenceDbContextFactory.CreateDbContext(databaseName);
        var repo = new OrderQueryRepository(queryDb, logger.Object);

        var result = await repo.GetByCorrelationIdAsync("corr-1");

        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle(i => i.Id == activeItem.Id);
        result.Items.First().Product.Should().NotBeNull();
    }

    [Fact]
    public async Task CountByCustomerIdAsync_ReturnsCount()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<OrderQueryRepository>>();
        var repo = new OrderQueryRepository(db, logger.Object);

        db.Orders.AddRange(
            new Order { Id = 1, CustomerId = 55, CorrelationId = "c-1" },
            new Order { Id = 2, CustomerId = 55, CorrelationId = "c-2" },
            new Order { Id = 3, CustomerId = 77, CorrelationId = "c-3" });
        await db.SaveChangesAsync();

        var count = await repo.CountByCustomerIdAsync(55);

        count.Should().Be(2);
    }
}
