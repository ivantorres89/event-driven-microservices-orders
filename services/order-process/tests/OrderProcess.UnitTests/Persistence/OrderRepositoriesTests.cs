using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Impl.Repositories;
using OrderProcess.UnitTests.Helpers;

namespace OrderProcess.UnitTests.Persistence;

public sealed class OrderRepositoriesTests
{
    private static Customer NewCustomer(string externalId)
        => new()
        {
            ExternalCustomerId = externalId,
            FirstName = "Alice",
            LastName = "Johnson",
            Email = "alice.johnson@contoso.demo",
            PhoneNumber = "+1-206-555-0001",
            NationalId = "NID-0001",
            AddressLine1 = "100 Pike St",
            City = "Seattle",
            PostalCode = "98101",
            CountryCode = "US"
        };

    private static Product NewProduct(string externalId, string name)
        => new()
        {
            ExternalProductId = externalId,
            Name = name,
            Category = "Billing",
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 9.99m,
            IsActive = true
        };

    [Fact]
    public async Task GetByCorrelationIdAsync_WhenOrderExists_ReturnsOrderWithChildrenIncluded()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await using (var seedDb = EfTestDb.Create(dbName))
        {
            var customer = NewCustomer("cust-1");
            var product1 = NewProduct("prod-1", "Contoso Easy Invoice — Pro (Monthly)");
            var product2 = NewProduct("prod-2", "Contoso Tax Calendar — Annual");
            product2.Category = "Tax";
            product2.BillingPeriod = "Annual";
            product2.Price = 99m;

            var order = new Order
            {
                CorrelationId = "corr-123",
                Customer = customer,
                Items = new List<OrderItem>
                {
                    new() { Product = product1, Quantity = 2 },
                    new() { Product = product2, Quantity = 1 }
                }
            };

            seedDb.Orders.Add(order);
            await seedDb.SaveChangesAsync();
        }

        await using var db = EfTestDb.Create(dbName);
        var logger = Mock.Of<ILogger<OrderQueryRepository>>();
        var repo = new OrderQueryRepository(db, logger);

        // Act
        var result = await repo.GetByCorrelationIdAsync("corr-123");

        // Assert
        result.Should().NotBeNull();
        result!.Customer.Should().NotBeNull();
        result.Customer!.ExternalCustomerId.Should().Be("cust-1");

        result.Items.Should().HaveCount(2);
        result.Items.All(i => i.Product is not null).Should().BeTrue();
        result.Items.Select(i => i.Product!.ExternalProductId).Should().BeEquivalentTo(new[] { "prod-1", "prod-2" });
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_WhenOrderDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<OrderQueryRepository>>();
        var repo = new OrderQueryRepository(db, logger);

        // Act
        var result = await repo.GetByCorrelationIdAsync("missing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Add_WhenOrderHasItems_PersistsAggregateOnSaveChanges()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var logger = Mock.Of<ILogger<OrderCommandRepository>>();
        var repo = new OrderCommandRepository(db, logger);

        var order = new Order
        {
            CorrelationId = "corr-add",
            Customer = NewCustomer("cust-add"),
            Items = new List<OrderItem>
            {
                new() { Product = NewProduct("prod-add-1", "Contoso Receipt Capture — Monthly"), Quantity = 1 },
                new() { Product = NewProduct("prod-add-2", "Contoso Sales Dashboard — Annual"), Quantity = 3 }
            }
        };
        order.Items.ElementAt(1).Product!.Category = "Reporting";
        order.Items.ElementAt(1).Product!.BillingPeriod = "Annual";
        order.Items.ElementAt(1).Product!.Price = 199m;

        // Act
        repo.Add(order);
        await db.SaveChangesAsync();

        // Assert
        var persisted = await db.Orders.Include(o => o.Items).SingleAsync(o => o.CorrelationId == "corr-add");
        persisted.Items.Should().HaveCount(2);
        (await db.Products.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Delete_WhenDeletingOrder_CascadesToOrderItems()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var order = new Order
        {
            CorrelationId = "corr-del",
            Customer = NewCustomer("cust-del"),
            Items = new List<OrderItem>
            {
                new() { Product = NewProduct("prod-del", "Contoso Payment Reminders — Monthly"), Quantity = 1 }
            }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var logger = Mock.Of<ILogger<OrderCommandRepository>>();
        var repo = new OrderCommandRepository(db, logger);
        var tracked = await db.Orders.Include(o => o.Items).SingleAsync(o => o.CorrelationId == "corr-del");

        // Act
        repo.Delete(tracked);
        await db.SaveChangesAsync();

        // Assert
        (await db.Orders.CountAsync()).Should().Be(0);
        (await db.OrderItems.CountAsync()).Should().Be(0);
    }
}
