using FluentAssertions;
using Moq;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Events;
using OrderAccept.Application.Contracts.Requests;
using OrderAccept.Application.Handlers;
using OrderAccept.Shared.Correlation;

namespace OrderAccept.UnitTests.Handlers;

public sealed class AcceptOrderHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenCalled_PublishesOrderAcceptedEvent_AndSetsWorkflowStatus()
    {
        // Arrange
        var publisherMock = new Mock<IMessagePublisher>();
        var workflowMock = new Mock<IOrderWorkflowStateStore>();

        var handler = new AcceptOrderHandler(
            publisherMock.Object,
            workflowMock.Object);

        var correlationId = CorrelationId.New();
        var request = new CreateOrderRequest(
            CustomerId: "customer-123",
            Items: new[]
            {
                new CreateOrderItem("product-1", 2)
            });

        var command = new AcceptOrderCommand(request, correlationId);

        // Act
        await handler.HandleAsync(command);

        // Assert
        workflowMock.Verify(
            s => s.SetStatusAsync(
                correlationId,
                OrderAccept.Shared.Workflow.OrderWorkflowStatus.Accepted,
                It.IsAny<CancellationToken>()),
            Times.Once);

        publisherMock.Verify(
            p => p.PublishAsync(
                It.Is<OrderAcceptedEvent>(e =>
                    e.Order == request &&
                    e.CorrelationId == correlationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPublishFails_RemovesWorkflowStatus()
    {
        // Arrange
        var publisherMock = new Mock<IMessagePublisher>();
        var workflowMock = new Mock<IOrderWorkflowStateStore>();

        publisherMock
            .Setup(p => p.PublishAsync(It.IsAny<OrderAcceptedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));

        var handler = new AcceptOrderHandler(
            publisherMock.Object,
            workflowMock.Object);

        var correlationId = CorrelationId.New();
        var request = new CreateOrderRequest(
            CustomerId: "customer-123",
            Items: new[]
            {
                new CreateOrderItem("product-1", 2)
            });

        var command = new AcceptOrderCommand(request, correlationId);

        // Act
        var act = async () => await handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        workflowMock.Verify(
            s => s.RemoveStatusAsync(
                correlationId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
