using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrderNotification.Application.Commands;
using OrderNotification.Application.Contracts.Events;
using OrderNotification.Application.Exceptions;
using OrderNotification.Application.Handlers;
using OrderNotification.Infrastructure.Messaging;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Resilience;

namespace OrderNotification.UnitTests.Infraestructure;

public sealed class ServiceBusOrderProcessedMessageListenerTests
{
    [Fact]
    public async Task ProcessInboundAsync_WhenValidMessage_Completes()
    {
        var correlationId = CorrelationId.New();
        var message = new OrderProcessedEvent(correlationId, 77);
        var body = JsonSerializer.Serialize(message);

        var handler = new Mock<INotifyOrderProcessedHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<NotifyOrderProcessedCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeFactory = BuildScopeFactory(handler.Object);
        var listener = BuildListener(scopeFactory);

        var actions = new Mock<IServiceBusMessageActions>();
        actions.Setup(a => a.CompleteAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await listener.ProcessInboundAsync(body, new Dictionary<string, object>(), actions.Object, CancellationToken.None);

        actions.Verify(a => a.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
        actions.Verify(a => a.AbandonAsync(It.IsAny<CancellationToken>()), Times.Never);
        actions.Verify(a => a.DeadLetterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessInboundAsync_WhenInvalidMessage_DeadLetters()
    {
        var message = new OrderProcessedEvent(new CorrelationId(Guid.Empty), 0);
        var body = JsonSerializer.Serialize(message);

        var handler = new Mock<INotifyOrderProcessedHandler>();
        var scopeFactory = BuildScopeFactory(handler.Object);
        var listener = BuildListener(scopeFactory);

        var actions = new Mock<IServiceBusMessageActions>();
        actions.Setup(a => a.DeadLetterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await listener.ProcessInboundAsync(body, new Dictionary<string, object>(), actions.Object, CancellationToken.None);

        actions.Verify(a => a.DeadLetterAsync("invalid_payload", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInboundAsync_WhenDependencyUnavailable_Abandons()
    {
        var correlationId = CorrelationId.New();
        var message = new OrderProcessedEvent(correlationId, 77);
        var body = JsonSerializer.Serialize(message);

        var handler = new Mock<INotifyOrderProcessedHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<NotifyOrderProcessedCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DependencyUnavailableException("redis"));

        var scopeFactory = BuildScopeFactory(handler.Object);
        var listener = BuildListener(scopeFactory);

        var actions = new Mock<IServiceBusMessageActions>();
        actions.Setup(a => a.AbandonAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await listener.ProcessInboundAsync(body, new Dictionary<string, object>(), actions.Object, CancellationToken.None);

        actions.Verify(a => a.AbandonAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IServiceScopeFactory BuildScopeFactory(INotifyOrderProcessedHandler handler)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(INotifyOrderProcessedHandler))).Returns(handler);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return factory.Object;
    }

    private static ServiceBusOrderProcessedMessageListener BuildListener(IServiceScopeFactory scopeFactory)
    {
        var options = Options.Create(new ServiceBusOptions
        {
            ConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc",
            InboundQueueName = "order.processed"
        });

        return new ServiceBusOrderProcessedMessageListener(options, scopeFactory, NullLogger<ServiceBusOrderProcessedMessageListener>.Instance);
    }
}
