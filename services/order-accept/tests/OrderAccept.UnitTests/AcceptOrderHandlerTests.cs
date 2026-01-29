using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Events;
using OrderAccept.Application.Contracts.Requests;
using OrderAccept.Application.Handlers;
using OrderAccept.Shared.Correlation;
using OrderAccept.Shared.Workflow;

namespace OrderAccept.UnitTests;

public sealed class AcceptOrderHandlerTests
{
    Guid _correlationId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_WhenRequestIsValid_SetsAcceptedStatusAndPublishesEvent()
    {
        // Arrange
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var stateStore = new Mock<IOrderWorkflowStateStore>(MockBehavior.Strict);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<AcceptOrderHandler>>();

        var request = new CreateOrderRequest(
            CustomerId: "customer-123",
            Items: new[] { new CreateOrderItem("product-1", 2) });

        var command = new AcceptOrderCommand(request);

        stateStore
            .Setup(s => s.SetStatusAsync(It.IsAny<CorrelationId>(), OrderWorkflowStatus.Accepted, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher
            .Setup(p => p.PublishAsync(It.IsAny<OrderAcceptedEvent>(), null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        correlationIdProvider
            .Setup(c => c.GetCorrelationId()).Returns(new CorrelationId(_correlationId));

        var handler = new AcceptOrderHandler(
            publisher.Object, stateStore.Object, correlationIdProvider.Object, logger);

        // Act
        await handler.HandleAsync(command);

        // Assert
        stateStore.Verify(s =>
            s.SetStatusAsync(
                It.IsAny<CorrelationId>(),
                OrderWorkflowStatus.Accepted,
                It.IsAny<CancellationToken>()),
            Times.Once);

        publisher.Verify(p =>
            p.PublishAsync(
                It.Is<OrderAcceptedEvent>(e =>
                    e.Order == request),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStateStoreFails_DoesNotPublishEvent()
    {
        // Arrange
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var stateStore = new Mock<IOrderWorkflowStateStore>(MockBehavior.Strict);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<AcceptOrderHandler>>();

        var request = new CreateOrderRequest("customer-123", new[] { new CreateOrderItem("product-1", 1) });
        
        var command = new AcceptOrderCommand(request);

        stateStore
            .Setup(s => s.SetStatusAsync(It.IsAny<CorrelationId>(), OrderWorkflowStatus.Accepted, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        correlationIdProvider
            .Setup(c => c.GetCorrelationId()).Returns(new CorrelationId(_correlationId));

        var handler = new AcceptOrderHandler(
            publisher.Object, stateStore.Object, correlationIdProvider.Object, logger);

        // Act
        var act = async () => await handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Redis down");

        publisher.Verify(p => p.PublishAsync(
            It.IsAny<OrderAcceptedEvent>(), null, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPublishFails_RemovesTransientStateAndRethrows()
    {
        // Arrange
        var publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var stateStore = new Mock<IOrderWorkflowStateStore>(MockBehavior.Strict);
        var correlationIdProvider = new Mock<ICorrelationIdProvider>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<AcceptOrderHandler>>();

        var request = new CreateOrderRequest("customer-123", new[] { new CreateOrderItem("product-1", 1) });
        
        var command = new AcceptOrderCommand(request);

        stateStore
            .Setup(s => s.SetStatusAsync(It.IsAny<CorrelationId>(), OrderWorkflowStatus.Accepted, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        stateStore
            .Setup(s => s.RemoveStatusAsync(It.IsAny<CorrelationId>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        publisher
            .Setup(p => p.PublishAsync(
                It.IsAny<OrderAcceptedEvent>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Broker down"));

        correlationIdProvider
            .Setup(c => c.GetCorrelationId()).Returns(new CorrelationId(_correlationId));

        var handler = new AcceptOrderHandler(publisher.Object, stateStore.Object, correlationIdProvider.Object, logger);

        // Act
        var act = async () => await handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Broker down");

        stateStore.Verify(s => s.RemoveStatusAsync(It.IsAny<CorrelationId>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
