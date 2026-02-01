using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcess.Application.Abstractions;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Contracts.Events;
using OrderProcess.Application.Contracts.Requests;
using OrderProcess.Application.Handlers;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Shared.Correlation;
using OrderProcess.Shared.Workflow;

namespace OrderProcess.UnitTests;

public sealed class ProcessOrderHandlerTests
{
    private static ProcessOrderCommand CreateCommand(CorrelationId correlationId)
    {
        var request = new CreateOrderRequest(
            CustomerId: "customer-123",
            Items: new[]
            {
                new CreateOrderItem("product-1", 2),
                new CreateOrderItem("product-2", 1)
            });

        var accepted = new OrderAcceptedEvent(correlationId, request);
        return new ProcessOrderCommand(accepted);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageIsValid_SetsProcessingPersistsSetsCompletedAndPublishesEvent()
    {
        // Arrange
        var workflow = new Mock<IOrderWorkflowStateStore>(MockBehavior.Strict);
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<ProcessOrderHandler>>();

        var customers = new Mock<ICustomerRepository>(MockBehavior.Strict);
        var orders = new Mock<IOrderRepository>(MockBehavior.Strict);
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        var orderItems = new Mock<IOrderItemRepository>(MockBehavior.Strict);

        uow.SetupGet(x => x.Customers).Returns(customers.Object);
        uow.SetupGet(x => x.Orders).Returns(orders.Object);
        uow.SetupGet(x => x.Products).Returns(products.Object);
        uow.SetupGet(x => x.OrderItems).Returns(orderItems.Object);

        var correlationId = new CorrelationId(Guid.NewGuid());
        correlationIdProvider.Setup(c => c.GetCorrelationId()).Returns(correlationId);

        var command = CreateCommand(correlationId);

        workflow
            .Setup(s => s.SetStatusAsync(correlationId, OrderWorkflowStatus.Processing, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workflow
            .Setup(s => s.SetCompletedAsync(correlationId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        orders
            .Setup(r => r.GetByCorrelationIdAsync(correlationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        customers
            .Setup(r => r.GetByExternalIdAsync(command.Event.Order.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        Customer? capturedCustomer = null;
        Order? capturedOrder = null;
        var capturedItems = new List<OrderItem>();

        customers
            .Setup(r => r.Add(It.IsAny<Customer>()))
            .Callback<Customer>(c => capturedCustomer = c);

        orders
            .Setup(r => r.Add(It.IsAny<Order>()))
            .Callback<Order>(o => capturedOrder = o);

        products
            .Setup(r => r.GetByExternalIdAsync("product-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        products
            .Setup(r => r.GetByExternalIdAsync("product-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        products
            .Setup(r => r.Add(It.IsAny<Product>()))
            .Callback<Product>(_ => { });

        orderItems
            .Setup(r => r.Add(It.IsAny<OrderItem>()))
            .Callback<OrderItem>(i => capturedItems.Add(i));

        uow
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // Simulate identity generation during SaveChanges
                if (capturedOrder is not null)
                    capturedOrder.Id = 42;
            })
            .ReturnsAsync(1);

        publisher
            .Setup(p => p.PublishAsync(It.IsAny<OrderProcessedEvent>(), null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ProcessOrderHandler(workflow.Object, uow.Object, publisher.Object, correlationIdProvider.Object, logger);

        // Act
        await handler.HandleAsync(command);

        // Assert
        workflow.Verify(s => s.SetStatusAsync(correlationId, OrderWorkflowStatus.Processing, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        workflow.Verify(s => s.SetCompletedAsync(correlationId, 42, It.IsAny<CancellationToken>()), Times.Once);

        publisher.Verify(p => p.PublishAsync(
            It.Is<OrderProcessedEvent>(e => e.CorrelationId == correlationId && e.OrderId == 42),
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        capturedCustomer.Should().NotBeNull();
        capturedOrder.Should().NotBeNull();
        capturedItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_WhenOrderAlreadyExists_DoesNotInsertButStillPublishes()
    {
        // Arrange
        var workflow = new Mock<IOrderWorkflowStateStore>(MockBehavior.Strict);
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<ProcessOrderHandler>>();

        var customers = new Mock<ICustomerRepository>(MockBehavior.Strict);
        var orders = new Mock<IOrderRepository>(MockBehavior.Strict);
        var products = new Mock<IProductRepository>(MockBehavior.Strict);
        var orderItems = new Mock<IOrderItemRepository>(MockBehavior.Strict);

        uow.SetupGet(x => x.Customers).Returns(customers.Object);
        uow.SetupGet(x => x.Orders).Returns(orders.Object);
        uow.SetupGet(x => x.Products).Returns(products.Object);
        uow.SetupGet(x => x.OrderItems).Returns(orderItems.Object);

        var correlationId = new CorrelationId(Guid.NewGuid());
        correlationIdProvider.Setup(c => c.GetCorrelationId()).Returns(correlationId);

        var command = CreateCommand(correlationId);

        workflow
            .Setup(s => s.SetStatusAsync(correlationId, OrderWorkflowStatus.Processing, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workflow
            .Setup(s => s.SetCompletedAsync(correlationId, 123, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        orders
            .Setup(r => r.GetByCorrelationIdAsync(correlationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = 123, CorrelationId = correlationId.ToString() });

        publisher
            .Setup(p => p.PublishAsync(It.IsAny<OrderProcessedEvent>(), null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ProcessOrderHandler(workflow.Object, uow.Object, publisher.Object, correlationIdProvider.Object, logger);

        // Act
        await handler.HandleAsync(command);

        // Assert
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        customers.Verify(r => r.Add(It.IsAny<Customer>()), Times.Never);
        products.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
        orderItems.Verify(r => r.Add(It.IsAny<OrderItem>()), Times.Never);

        publisher.Verify(p => p.PublishAsync(
            It.Is<OrderProcessedEvent>(e => e.CorrelationId == correlationId && e.OrderId == 123),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStateStoreFails_DoesNotPersistOrPublish()
    {
        // Arrange
        var workflow = new Mock<IOrderWorkflowStateStore>(MockBehavior.Strict);
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<ProcessOrderHandler>>();

        var correlationId = new CorrelationId(Guid.NewGuid());
        correlationIdProvider.Setup(c => c.GetCorrelationId()).Returns(correlationId);

        var command = CreateCommand(correlationId);

        workflow
            .Setup(s => s.SetStatusAsync(correlationId, OrderWorkflowStatus.Processing, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        var handler = new ProcessOrderHandler(workflow.Object, uow.Object, publisher.Object, correlationIdProvider.Object, logger);

        // Act
        var act = async () => await handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Redis down");

        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(p => p.PublishAsync(It.IsAny<OrderProcessedEvent>(), null, It.IsAny<CancellationToken>()), Times.Never);
    }
}
