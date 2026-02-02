using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Impl.Repositories;
using OrderProcess.UnitTests.Helpers;

namespace OrderProcess.UnitTests.Persistence;

public sealed class OrderItemRepositoriesTests
{
    private static Customer NewCustomer(string externalId)
        => new()
        {
            ExternalCustomerId = externalId,
            FirstName = "Bob",
            LastName = "Smith",
            Email = "bob.smith@contoso.demo",
            PhoneNumber = "+1-415-555-0002",
            NationalId = "NID-0002",
            AddressLine1 = "200 Market St",
            City = "San Francisco",
            PostalCode = "94105",
            CountryCode = "US"
        };

    private static Product NewProduct(string externalId)
        => new()
        {
            ExternalProductId = externalId,
            Name = "Contoso Multi-user (up to 3) — Monthly",
            Category = "Collaboration",
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 12m,
            IsActive = true
        };

    [Fact]
    public async Task FindAsync_WhenOrderItemExists_IncludesProduct()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        long itemId;
        await using (var seedDb = EfTestDb.Create(dbName))
        {
            var order = new Order
            {
                CorrelationId = "corr-item",
                Customer = NewCustomer("cust-item"),
                Items = new List<OrderItem>
                {
                    new() { Product = NewProduct("prod-item"), Quantity = 2 }
                }
            };

            seedDb.Orders.Add(order);
            await seedDb.SaveChangesAsync();
            itemId = await seedDb.OrderItems.Select(i => i.Id).SingleAsync();
        }

        await using var db = EfTestDb.Create(dbName);
        var logger = Mock.Of<ILogger<OrderItemQueryRepository>>();
        var repo = new OrderItemQueryRepository(db, logger);

        // Act
        var result = await repo.FindAsync(itemId);

        // Assert
        result.Should().NotBeNull();
        result!.Product.Should().NotBeNull();
        result.Product!.ExternalProductId.Should().Be("prod-item");
    }

    [Fact]
    public async Task FindAsync_WhenOrderItemDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<OrderItemQueryRepository>>();
        var repo = new OrderItemQueryRepository(db, logger);

        // Act
        var result = await repo.FindAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WhenDeletingOrderItem_RemovesOnlyTheItem()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var order = new Order
        {
            CorrelationId = "corr-del-item",
            Customer = NewCustomer("cust-del-item"),
            Items = new List<OrderItem>
            {
                new() { Product = NewProduct("prod-del-item"), Quantity = 1 }
            }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var item = await db.OrderItems.SingleAsync();
        var productId = item.ProductId;

        var logger = Mock.Of<ILogger<OrderItemCommandRepository>>();
        var repo = new OrderItemCommandRepository(db, logger);

        // Act
        repo.Delete(item);
        await db.SaveChangesAsync();

        // Assert
        (await db.OrderItems.CountAsync()).Should().Be(0);
        (await db.Products.CountAsync()).Should().Be(1);
        (await db.Products.AnyAsync(p => p.Id == productId)).Should().BeTrue();
        (await db.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FindAsync_WhenDbContextDisposed_Throws()
    {
        // Arrange
        var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<OrderItemQueryRepository>>();
        var repo = new OrderItemQueryRepository(db, logger);
        await db.DisposeAsync();

        // Act
        var act = async () => await repo.FindAsync(1);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
