using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Contracts.Events;
using OrderProcess.Application.Contracts.Requests;
using OrderProcess.Application.Handlers;
using OrderProcess.Infrastructure.Messaging;
using OrderProcess.Shared.Correlation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderProcess.UnitTests;

public sealed class RabbitMqOrderAcceptedMessageListenerTests
{
    [Fact]
    public async Task HandleMessageAsync_WhenPayloadIsValid_InvokesHandlerAndAcks()
    {
        // Arrange
        var correlationId = new CorrelationId(Guid.NewGuid());
        var accepted = new OrderAcceptedEvent(
            correlationId,
            new CreateOrderRequest("customer-123", new[] { new CreateOrderItem("product-1", 1) }));

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(accepted));

        var handler = new Mock<IProcessOrderHandler>(MockBehavior.Strict);
        handler.Setup(h => h.HandleAsync(
                It.IsAny<ProcessOrderCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(p => p.GetService(typeof(IProcessOrderHandler)))
            .Returns(handler.Object);

        var scope = new Mock<IServiceScope>(MockBehavior.Strict);
        scope.SetupGet(s => s.ServiceProvider).Returns(sp.Object);
        scope.Setup(s => s.Dispose());

        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            InboundQueueName = "order.accepted",
            MaxProcessingAttempts = 3
        });

        var logger = Mock.Of<ILogger<RabbitMqOrderAcceptedMessageListener>>();
        var listener = new RabbitMqOrderAcceptedMessageListener(options, scopeFactory.Object, logger);

        var channel = new Mock<IChannel>(MockBehavior.Strict);
        channel.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var ea = new BasicDeliverEventArgs
        {
            DeliveryTag = 1,
            Body = body,
            BasicProperties = new BasicProperties
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, object>()
            }
        };

        try
        {
            // Act
            await listener.HandleMessageAsync(channel.Object, ea, CancellationToken.None);

            // Assert
            handler.Verify(h => h.HandleAsync(
                    It.Is<ProcessOrderCommand>(c => c.Event == accepted),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            channel.Verify(c => c.BasicAckAsync(1, false, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            CorrelationContext.Current = null;
            Baggage.SetBaggage("correlation_id", null);
        }
    }

    [Fact]
    public async Task HandleMessageAsync_WhenPayloadIsInvalidJson_RejectsWithoutRequeue()
    {
        // Arrange
        var handler = new Mock<IProcessOrderHandler>(MockBehavior.Strict);

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(p => p.GetService(typeof(IProcessOrderHandler)))
            .Returns(handler.Object);

        var scope = new Mock<IServiceScope>(MockBehavior.Strict);
        scope.SetupGet(s => s.ServiceProvider).Returns(sp.Object);
        scope.Setup(s => s.Dispose());

        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            InboundQueueName = "order.accepted",
            MaxProcessingAttempts = 3
        });

        var logger = Mock.Of<ILogger<RabbitMqOrderAcceptedMessageListener>>();
        var listener = new RabbitMqOrderAcceptedMessageListener(options, scopeFactory.Object, logger);

        var channel = new Mock<IChannel>(MockBehavior.Strict);
        channel.Setup(c => c.BasicRejectAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var ea = new BasicDeliverEventArgs
        {
            DeliveryTag = 1,
            Body = Encoding.UTF8.GetBytes("{ not-json"),
            BasicProperties = new BasicProperties
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, object>()
            }
        };

        // Act
        await listener.HandleMessageAsync(channel.Object, ea, CancellationToken.None);

        // Assert
        channel.Verify(c => c.BasicRejectAsync(1, false, It.IsAny<CancellationToken>()), Times.Once);
        handler.Verify(h => h.HandleAsync(It.IsAny<ProcessOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_WhenHandlerFailsAndRetriesAvailable_RepublishesWithIncrementedRetryHeaderAndAcks()
    {
        // Arrange
        var correlationId = new CorrelationId(Guid.NewGuid());
        var accepted = new OrderAcceptedEvent(
            correlationId,
            new CreateOrderRequest("customer-123", new[] { new CreateOrderItem("product-1", 1) }));

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(accepted));

        var handler = new Mock<IProcessOrderHandler>(MockBehavior.Strict);
        handler.Setup(h => h.HandleAsync(It.IsAny<ProcessOrderCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(p => p.GetService(typeof(IProcessOrderHandler)))
            .Returns(handler.Object);

        var scope = new Mock<IServiceScope>(MockBehavior.Strict);
        scope.SetupGet(s => s.ServiceProvider).Returns(sp.Object);
        scope.Setup(s => s.Dispose());

        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            InboundQueueName = "order.accepted",
            MaxProcessingAttempts = 3
        });

        var logger = Mock.Of<ILogger<RabbitMqOrderAcceptedMessageListener>>();
        var listener = new RabbitMqOrderAcceptedMessageListener(options, scopeFactory.Object, logger);

        var channel = new Mock<IChannel>(MockBehavior.Strict);

        BasicProperties? capturedProps = null;
        channel.Setup(c => c.BasicPublishAsync(
                "",
                options.Value.InboundQueueName,
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, IReadOnlyBasicProperties, ReadOnlyMemory<byte>, CancellationToken>((_, _, _, props, _, _) =>
            {
                capturedProps = (BasicProperties?)props;
            })
            .Returns(ValueTask.CompletedTask);

        channel.Setup(c => c.BasicAckAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var headers = new Dictionary<string, object>();

        var ea = new BasicDeliverEventArgs
        {
            DeliveryTag = 1,
            Body = body,
            BasicProperties = new BasicProperties
            {
                ContentType = "application/json",
                Headers = headers
            }
        };

        // Act
        await listener.HandleMessageAsync(channel.Object, ea, CancellationToken.None);

        // Assert
        capturedProps.Should().NotBeNull();
        capturedProps!.Headers.Should().ContainKey("x-retry-count");
        capturedProps.Headers!["x-retry-count"].Should().Be(1);

        channel.Verify(c => c.BasicPublishAsync(
                "",
                options.Value.InboundQueueName,
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        channel.Verify(c => c.BasicAckAsync(1, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_WhenHandlerFailsAndMaxRetriesExceeded_RejectsWithoutRequeue()
    {
        // Arrange
        var correlationId = new CorrelationId(Guid.NewGuid());
        var accepted = new OrderAcceptedEvent(
            correlationId,
            new CreateOrderRequest("customer-123", new[] { new CreateOrderItem("product-1", 1) }));

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(accepted));

        var handler = new Mock<IProcessOrderHandler>(MockBehavior.Strict);
        handler.Setup(h => h.HandleAsync(It.IsAny<ProcessOrderCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var sp = new Mock<IServiceProvider>(MockBehavior.Strict);
        sp.Setup(p => p.GetService(typeof(IProcessOrderHandler)))
            .Returns(handler.Object);

        var scope = new Mock<IServiceScope>(MockBehavior.Strict);
        scope.SetupGet(s => s.ServiceProvider).Returns(sp.Object);
        scope.Setup(s => s.Dispose());

        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            InboundQueueName = "order.accepted",
            MaxProcessingAttempts = 2
        });

        var logger = Mock.Of<ILogger<RabbitMqOrderAcceptedMessageListener>>();
        var listener = new RabbitMqOrderAcceptedMessageListener(options, scopeFactory.Object, logger);

        var channel = new Mock<IChannel>(MockBehavior.Strict);
        channel.Setup(c => c.BasicRejectAsync(1, false, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var ea = new BasicDeliverEventArgs
        {
            DeliveryTag = 1,
            Body = body,
            BasicProperties = new BasicProperties
            {
                ContentType = "application/json",
                Headers = new Dictionary<string, object> { ["x-retry-count"] = 2 }
            }
        };

        // Act
        await listener.HandleMessageAsync(channel.Object, ea, CancellationToken.None);

        // Assert
        channel.Verify(c => c.BasicRejectAsync(1, false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
