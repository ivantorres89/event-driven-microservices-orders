using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderNotification.Application.Abstractions;
using OrderNotification.Application.Commands;
using OrderNotification.Application.Contracts.Events;
using OrderNotification.Application.Exceptions;
using OrderNotification.Application.Handlers;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.UnitTests.Application;

public sealed class NotifyOrderProcessedHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenUserResolved_NotifiesCompletedStatus()
    {
        var correlationId = CorrelationId.New();
        var orderId = 123L;
        var userId = "user-123";

        var registry = new Mock<IOrderCorrelationRegistry>();
        registry.Setup(r => r.ResolveUserIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);

        var notifier = new Mock<IOrderStatusNotifier>();
        notifier.Setup(n => n.NotifyStatusChangedAsync(
                userId,
                correlationId,
                OrderWorkflowStatus.Completed,
                orderId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var correlationProvider = new Mock<ICorrelationIdProvider>();
        correlationProvider.Setup(p => p.GetCorrelationId()).Returns(CorrelationId.New());

        var handler = new NotifyOrderProcessedHandler(
            registry.Object,
            notifier.Object,
            correlationProvider.Object,
            NullLogger<NotifyOrderProcessedHandler>.Instance);

        var command = new NotifyOrderProcessedCommand(new OrderProcessedEvent(correlationId, orderId));

        await handler.HandleAsync(command);

        notifier.Verify(n => n.NotifyStatusChangedAsync(
            userId,
            correlationId,
            OrderWorkflowStatus.Completed,
            orderId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenUserNotResolved_ThrowsCorrelationMappingNotFound()
    {
        var correlationId = CorrelationId.New();
        var orderId = 10L;

        var registry = new Mock<IOrderCorrelationRegistry>();
        registry.Setup(r => r.ResolveUserIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var notifier = new Mock<IOrderStatusNotifier>();
        var correlationProvider = new Mock<ICorrelationIdProvider>();
        correlationProvider.Setup(p => p.GetCorrelationId()).Returns(CorrelationId.New());

        var handler = new NotifyOrderProcessedHandler(
            registry.Object,
            notifier.Object,
            correlationProvider.Object,
            NullLogger<NotifyOrderProcessedHandler>.Instance);

        var command = new NotifyOrderProcessedCommand(new OrderProcessedEvent(correlationId, orderId));

        var act = () => handler.HandleAsync(command);

        await act.Should().ThrowAsync<CorrelationMappingNotFoundException>();
    }
}
