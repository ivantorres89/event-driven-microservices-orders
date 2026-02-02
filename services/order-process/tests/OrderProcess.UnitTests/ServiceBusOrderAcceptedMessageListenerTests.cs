using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Contracts.Events;
using OrderProcess.Application.Contracts.Requests;
using OrderProcess.Application.Handlers;
using OrderProcess.Infrastructure.Messaging;
using OrderProcess.Shared.Correlation;
using OrderProcess.Shared.Resilience;

namespace OrderProcess.UnitTests;

public sealed class ServiceBusOrderAcceptedMessageListenerTests
{
    private static ServiceBusOrderAcceptedMessageListener CreateListener(
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceBusOrderAcceptedMessageListener> logger)
    {
        var options = Options.Create(new ServiceBusOptions
        {
            // Valid-looking connection string; the listener ctor will not connect unless processing starts.
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=not-a-real-key=",
            InboundQueueName = "order.accepted",
            OutboundQueueName = "order.processed"
        });

        return new ServiceBusOrderAcceptedMessageListener(options, scopeFactory, logger);
    }

    [Fact]
    public async Task ProcessInboundAsync_WhenPayloadIsValid_InvokesHandlerAndCompletes()
    {
        // Arrange
        var correlationId = new CorrelationId(Guid.NewGuid());
        var accepted = new OrderAcceptedEvent(
            correlationId,
            new CreateOrderRequest("customer-123", new[] { new CreateOrderItem("product-1", 1) }));

        var handler = new Mock<IProcessOrderHandler>(MockBehavior.Strict);
        handler.Setup(h => h.HandleAsync(It.IsAny<ProcessOrderCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(p => p.GetService(typeof(IProcessOrderHandler))).Returns(handler.Object);

        var scope = new Mock<IServiceScope>(MockBehavior.Strict);
        scope.SetupGet(s => s.ServiceProvider).Returns(sp.Object);
        scope.Setup(s => s.Dispose());

        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var logger = Mock.Of<ILogger<ServiceBusOrderAcceptedMessageListener>>();
        var listener = CreateListener(scopeFactory.Object, logger);

        var actions = new Mock<IServiceBusMessageActions>(MockBehavior.Strict);
        actions.Setup(a => a.CompleteAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await listener.ProcessInboundAsync(
            body: JsonSerializer.Serialize(accepted),
            applicationProperties: new Dictionary<string, object>(),
            actions: actions.Object,
            cancellationToken: CancellationToken.None);

        // Assert
        handler.Verify(h => h.HandleAsync(
            It.Is<ProcessOrderCommand>(c => c.Event == accepted),
            It.IsAny<CancellationToken>()), Times.Once);

        actions.Verify(a => a.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInboundAsync_WhenPayloadIsInvalidJson_DeadLettersAndDoesNotInvokeHandler()
    {
        // Arrange
        var handler = new Mock<IProcessOrderHandler>(MockBehavior.Strict);

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(p => p.GetService(typeof(IProcessOrderHandler))).Returns(handler.Object);

        var scope = new Mock<IServiceScope>(MockBehavior.Strict);
        scope.SetupGet(s => s.ServiceProvider).Returns(sp.Object);
        scope.Setup(s => s.Dispose());

        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var logger = Mock.Of<ILogger<ServiceBusOrderAcceptedMessageListener>>();
        var listener = CreateListener(scopeFactory.Object, logger);

        var actions = new Mock<IServiceBusMessageActions>(MockBehavior.Strict);
        actions.Setup(a => a.DeadLetterAsync("invalid_payload", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await listener.ProcessInboundAsync(
            body: "{ not-json",
            applicationProperties: new Dictionary<string, object>(),
            actions: actions.Object,
            cancellationToken: CancellationToken.None);

        // Assert
        actions.Verify(a => a.DeadLetterAsync("invalid_payload", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        handler.Verify(h => h.HandleAsync(It.IsAny<ProcessOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessInboundAsync_WhenDependencyUnavailable_AbandonsForRetry()
    {
        // Arrange
        var correlationId = new CorrelationId(Guid.NewGuid());
        var accepted = new OrderAcceptedEvent(
            correlationId,
            new CreateOrderRequest("customer-123", new[] { new CreateOrderItem("product-1", 1) }));

        var handler = new Mock<IProcessOrderHandler>(MockBehavior.Strict);
        handler.Setup(h => h.HandleAsync(It.IsAny<ProcessOrderCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DependencyUnavailableException("SQL down"));

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(p => p.GetService(typeof(IProcessOrderHandler))).Returns(handler.Object);

        var scope = new Mock<IServiceScope>(MockBehavior.Strict);
        scope.SetupGet(s => s.ServiceProvider).Returns(sp.Object);
        scope.Setup(s => s.Dispose());

        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var logger = Mock.Of<ILogger<ServiceBusOrderAcceptedMessageListener>>();
        var listener = CreateListener(scopeFactory.Object, logger);

        var actions = new Mock<IServiceBusMessageActions>(MockBehavior.Strict);
        actions.Setup(a => a.AbandonAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await listener.ProcessInboundAsync(
            body: JsonSerializer.Serialize(accepted),
            applicationProperties: new Dictionary<string, object>(),
            actions: actions.Object,
            cancellationToken: CancellationToken.None);

        // Assert
        actions.Verify(a => a.AbandonAsync(It.IsAny<CancellationToken>()), Times.Once);
        actions.Verify(a => a.CompleteAsync(It.IsAny<CancellationToken>()), Times.Never);
        actions.Verify(a => a.DeadLetterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
