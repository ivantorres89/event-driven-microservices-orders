using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Events;
using OrderAccept.Application.Contracts.Requests;
using OrderAccept.Application.Contracts.Responses;
using OrderAccept.Application.Handlers;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Command;
using OrderAccept.Persistence.Abstractions.Repositories.Query;
using OrderAccept.Shared.Correlation;
using OrderAccept.Shared.Workflow;

namespace OrderAccept.UnitTests.Application.Handlers;

public sealed class AcceptOrderHandlerTests
{
    private readonly CorrelationId _correlationId = new(Guid.NewGuid());

    [Fact]
    public async Task HandleAsync_WhenRequestIsValid_PersistsOrder_ReturnsDto_And_TriggersBestEffortSideEffects()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var productQueries = new Mock<IProductQueryRepository>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var customerCommands = new Mock<ICustomerCommandRepository>(MockBehavior.Strict);
        var orderCommands = new Mock<IOrderCommandRepository>(MockBehavior.Strict);

        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var workflow = new Mock<IOrderWorkflowStateStore>(MockBehavior.Strict);
        var correlationMap = new Mock<IOrderCorrelationMapStore>(MockBehavior.Strict);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<AcceptOrderHandler>>();

        var now = DateTime.UtcNow;

        var product = new Product
        {
            Id = 42,
            ExternalProductId = "product-1",
            Name = "Contoso Billing",
            Category = "Billing",
            Vendor = "Contoso",
            ImageUrl = "https://img.example/p1.png",
            Discount = 0,
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 19.99m,
            IsActive = true
        };

        var request = new CreateOrderRequest(
            CustomerId: "customer-123",
            Items: new[] { new CreateOrderItem(ProductId: "product-1", Quantity: 2) });

        var command = new AcceptOrderCommand(
            ExternalCustomerId: "customer-123",
            Order: request);

        Order? capturedOrder = null;

        // UoW wiring
        uow.SetupGet(x => x.ProductQueries).Returns(productQueries.Object);
        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);
        uow.SetupGet(x => x.CustomerCommands).Returns(customerCommands.Object);
        uow.SetupGet(x => x.OrderCommands).Returns(orderCommands.Object);

        productQueries
            .Setup(q => q.GetByExternalIdsAsync(
                It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "product-1" })),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("customer-123", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        customerCommands
            .Setup(c => c.Add(It.Is<Customer>(x => x.ExternalCustomerId == "customer-123")));

        orderCommands
            .Setup(c => c.Add(It.IsAny<Order>()))
            .Callback<Order>(o => capturedOrder = o);

        uow
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // Simulate EF-generated values
                capturedOrder!.Id = 1001;
                capturedOrder!.CreatedAt = now;
            })
            .ReturnsAsync(1);

        // Best-effort side effects
        correlationIdProvider
            .Setup(c => c.GetCorrelationId())
            .Returns(_correlationId);

        correlationMap
            .Setup(m => m.SetUserIdAsync(_correlationId, "customer-123", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workflow
            .Setup(s => s.SetStatusAsync(_correlationId, OrderWorkflowStatus.Accepted, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher
            .Setup(p => p.PublishAsync(
                It.Is<OrderAcceptedEvent>(e =>
                    e.CorrelationId == _correlationId &&
                    e.Order.CustomerId == "customer-123" &&
                    e.Order.Items.Single().ProductId == "product-1" &&
                    e.Order.Items.Single().Quantity == 2),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AcceptOrderHandler(
            uow.Object,
            publisher.Object,
            workflow.Object,
            correlationMap.Object,
            correlationIdProvider.Object,
            logger);

            // Act
        var dto = await handler.HandleAsync(command);

        // Assert
        dto.Should().BeEquivalentTo(new OrderDto(
            Id: 1001,
            CorrelationId: _correlationId.ToString(),
            CreatedAt: now,
            Items: new[]
            {
                new OrderItemDto(
                    ProductId: "product-1",
                    ProductName: "Contoso Billing",
                    ImageUrl: "https://img.example/p1.png",
                    UnitPrice: 19.99m,
                    Quantity: 2)
            }));

        capturedOrder.Should().NotBeNull();
        capturedOrder!.Items.Should().ContainSingle(i => i.ProductId == 42 && i.Quantity == 2);

        correlationMap.VerifyAll();
        workflow.VerifyAll();
        publisher.VerifyAll();
        uow.VerifyAll();
        productQueries.VerifyAll();
        customerQueries.VerifyAll();
        customerCommands.VerifyAll();
        orderCommands.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_WhenAnyProductIsMissing_ThrowsProductNotFoundException()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var productQueries = new Mock<IProductQueryRepository>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);

        var publisher = new Mock<IMessagePublisher>(MockBehavior.Loose);
        var workflow = new Mock<IOrderWorkflowStateStore>(MockBehavior.Loose);
        var correlationMap = new Mock<IOrderCorrelationMapStore>(MockBehavior.Loose);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<AcceptOrderHandler>>();

        var request = new CreateOrderRequest(
            CustomerId: "customer-123",
            Items: new[]
            {
                new CreateOrderItem(ProductId: "missing-1", Quantity: 1),
                new CreateOrderItem(ProductId: "missing-2", Quantity: 1)
            });

        var command = new AcceptOrderCommand("customer-123", request);

        uow.SetupGet(x => x.ProductQueries).Returns(productQueries.Object);
        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);

        correlationIdProvider.Setup(c => c.GetCorrelationId()).Returns(_correlationId);

        // Return empty -> both missing
        productQueries
            .Setup(q => q.GetByExternalIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Product>());

        var handler = new AcceptOrderHandler(
            uow.Object,
            publisher.Object,
            workflow.Object,
            correlationMap.Object,
            correlationIdProvider.Object,
            logger);

        // Act
        var act = async () => await handler.HandleAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<ProductNotFoundException>();
        ex.Which.MissingExternalIds.Should().BeEquivalentTo(new[] { "missing-1", "missing-2" });
    }

    [Fact]
    public async Task HandleAsync_WhenBestEffortSideEffectsFail_StillReturnsOrderDto()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var productQueries = new Mock<IProductQueryRepository>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var customerCommands = new Mock<ICustomerCommandRepository>(MockBehavior.Loose);
        var orderCommands = new Mock<IOrderCommandRepository>(MockBehavior.Loose);

        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var workflow = new Mock<IOrderWorkflowStateStore>(MockBehavior.Strict);
        var correlationMap = new Mock<IOrderCorrelationMapStore>(MockBehavior.Strict);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<AcceptOrderHandler>>();

        var product = new Product
        {
            Id = 7,
            ExternalProductId = "product-1",
            Name = "P1",
            Category = "C",
            Vendor = "V",
            ImageUrl = "https://img/p1",
            Discount = 0,
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 10m,
            IsActive = true
        };

        var request = new CreateOrderRequest(
            CustomerId: "customer-123",
            Items: new[] { new CreateOrderItem("product-1", 1) });
        var command = new AcceptOrderCommand("customer-123", request);

        uow.SetupGet(x => x.ProductQueries).Returns(productQueries.Object);
        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);
        uow.SetupGet(x => x.CustomerCommands).Returns(customerCommands.Object);
        uow.SetupGet(x => x.OrderCommands).Returns(orderCommands.Object);

        productQueries
            .Setup(q => q.GetByExternalIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("customer-123", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        uow
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        correlationIdProvider.Setup(c => c.GetCorrelationId()).Returns(_correlationId);

        // All fail, but should not throw
        correlationMap
            .Setup(m => m.SetUserIdAsync(_correlationId, "customer-123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("redis down"));

        workflow
            .Setup(s => s.SetStatusAsync(_correlationId, OrderWorkflowStatus.Accepted, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("redis down"));

        publisher
            .Setup(p => p.PublishAsync(It.IsAny<OrderAcceptedEvent>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("broker down"));

        var handler = new AcceptOrderHandler(
            uow.Object,
            publisher.Object,
            workflow.Object,
            correlationMap.Object,
            correlationIdProvider.Object,
            logger);

        // Act
        var dto = await handler.HandleAsync(command);

        // Assert
        dto.Id.Should().BeGreaterThanOrEqualTo(0);
        dto.CorrelationId.Should().Be(_correlationId.ToString());
        dto.Items.Should().ContainSingle(i => i.ProductId == "product-1" && i.Quantity == 1);
    }
}
