using FluentAssertions;
using Microsoft.Azure.Amqp.Framing;
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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using OrderNotification.Shared.Resilience;

namespace OrderNotification.UnitTests.Infraestructure;

public sealed class RabbitMqOrderProcessedMessageListenerTests
{
    [Fact]
    public async Task HandleMessageAsync_WhenValidMessage_AcksAndInvokesHandler()
    {
        var correlationId = CorrelationId.New();
        var message = new OrderProcessedEvent(correlationId, 88);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var handler = new Mock<INotifyOrderProcessedHandler>(MockBehavior.Strict);
        handler.Setup(h => h.HandleAsync(
                It.IsAny<NotifyOrderProcessedCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeFactory = BuildScopeFactory(handler.Object);
        var listener = BuildListener(scopeFactory);

        var channel = new Mock<IChannel>();
        channel.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var headers = new Dictionary<string, object>();
        var ea = new BasicDeliverEventArgs(
            consumerTag: "",
            deliveryTag: 1,
            redelivered: false,
            exchange: "",
            routingKey: "",
            new BasicProperties
            {
                ContentType = "application/json",
                Headers = headers
            },
            body: body,
            cancellationToken: CancellationToken.None
        );

        await listener.HandleMessageAsync(channel.Object, ea, CancellationToken.None);

        handler.Verify(h => h.HandleAsync(It.IsAny<NotifyOrderProcessedCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicAckAsync(1, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicRejectAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_WhenInvalidPayload_RejectsWithoutRequeue()
    {
        var message = new OrderProcessedEvent(new CorrelationId(Guid.Empty), 0);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        var handler = new Mock<INotifyOrderProcessedHandler>();
        var scopeFactory = BuildScopeFactory(handler.Object);
        var listener = BuildListener(scopeFactory);

        var channel = new Mock<IChannel>();
        channel.Setup(c => c.BasicRejectAsync(It.IsAny<ulong>(), false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var headers = new Dictionary<string, object>();
        var ea = new BasicDeliverEventArgs(
            consumerTag: "",
            deliveryTag: 9,
            redelivered: false,
            exchange: "",
            routingKey: "",
            new BasicProperties
            {
                ContentType = "application/json",
                Headers = headers
            },
            body: body,
            cancellationToken: CancellationToken.None
        );

        await listener.HandleMessageAsync(channel.Object, ea, CancellationToken.None);

        channel.Verify(c => c.BasicRejectAsync(9, false, It.IsAny<CancellationToken>()), Times.Once);
        handler.Verify(h => h.HandleAsync(It.IsAny<NotifyOrderProcessedCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_WhenHandlerThrows_RetriesAndAcks()
    {
        var correlationId = CorrelationId.New();
        var message = new OrderProcessedEvent(correlationId, 42);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        var handler = new Mock<INotifyOrderProcessedHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<NotifyOrderProcessedCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DependencyUnavailableException("redis"));

        var scopeFactory = BuildScopeFactory(handler.Object);
        var listener = BuildListener(scopeFactory);
        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            InboundQueueName = "order.processed",
            MaxProcessingAttempts = 3
        });
        var channel = new Mock<IChannel>();
        channel.Setup(c => c.BasicPublishAsync(
                "",
                options.Value.InboundQueueName,
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
                  .Returns(ValueTask.CompletedTask);
        channel.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var headers = new Dictionary<string, object>();
        var ea = new BasicDeliverEventArgs(
            consumerTag: "",
            deliveryTag: 5,
            redelivered: false,
            exchange: "",
            routingKey: "",
            new BasicProperties
            {
                ContentType = "application/json",
                Headers = headers
            },
            body: body,
            cancellationToken: CancellationToken.None
        );

        await listener.HandleMessageAsync(channel.Object, ea, CancellationToken.None);

        channel.Verify(c => c.BasicPublishAsync(
                "",
                options.Value.InboundQueueName,
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
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

    private static RabbitMqOrderProcessedMessageListener BuildListener(IServiceScopeFactory scopeFactory)
    {
        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            InboundQueueName = "order.processed"
        });

        return new RabbitMqOrderProcessedMessageListener(options, scopeFactory, NullLogger<RabbitMqOrderProcessedMessageListener>.Instance);
    }
}
