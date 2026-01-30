using Castle.Core.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry;
using OrderAccept.Infrastructure.Messaging;
using OrderAccept.Infrastructure.Workflow;
using OrderAccept.Shared.Correlation;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace OrderAccept.UnitTests;

public sealed class RabbitMqMessagePublisherTests
{
    private sealed record TestMessage(string Id);

    [Fact]
    public async Task PublishAsync_WhenMessageIsValid_PublishesWithCorrelationHeader()
    {
        // Arrange
        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672",
            QueueName = "orders"
        });

        var factory = new Mock<IConnectionFactory>(MockBehavior.Strict);
        var connection = new Mock<IConnection>(MockBehavior.Strict);
        var channel = new Mock<IChannel>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<RabbitMqMessagePublisher>>();

        factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);

        connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        channel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        channel.Setup(c => c.QueueDeclareAsync(
                options.Value.QueueName,
                true,
                false,
                false,
                null,
                false,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk(options.Value.QueueName, 0, 0));

        BasicProperties? capturedProps = null;
        ReadOnlyMemory<byte> capturedBody = default;
        string? capturedRoutingKey = null;

        channel.Setup(c => c.BasicPublishAsync(
                "",
                It.IsAny<string>(),
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, IReadOnlyBasicProperties, ReadOnlyMemory<byte>, CancellationToken>((_, routingKey, _, props, body, _) =>
            {
                capturedRoutingKey = routingKey;
                capturedProps = (BasicProperties?)props;
                capturedBody = body;
            })
            .Returns(ValueTask.CompletedTask);

        var correlationId = new CorrelationId(Guid.NewGuid());
        CorrelationContext.Current = correlationId;

        try
        {
            var publisher = new RabbitMqMessagePublisher(options, logger, factory.Object);
            var message = new TestMessage("id-1");

            // Act
            await publisher.PublishAsync(message);

            // Assert
            capturedRoutingKey.Should().Be(options.Value.QueueName);
            capturedProps.Should().NotBeNull();
            capturedProps!.ContentType.Should().Be("application/json");
            capturedProps.Persistent.Should().BeTrue();
            capturedProps.Headers.Should().ContainKey("x-correlation-id");
            capturedProps.Headers!["x-correlation-id"]?.ToString().Should().Be(correlationId.Value.ToString());

            var json = Encoding.UTF8.GetString(capturedBody.ToArray());
            json.Should().Be(JsonSerializer.Serialize(message));
        }
        finally
        {
            CorrelationContext.Current = null;
            Baggage.SetBaggage("correlation_id", null);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenQueueDeclareFails_Throws()
    {
        // Arrange
        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672",
            QueueName = "orders"
        });

        var factory = new Mock<IConnectionFactory>(MockBehavior.Strict);
        var connection = new Mock<IConnection>(MockBehavior.Strict);
        var channel = new Mock<IChannel>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<RabbitMqMessagePublisher>>();

        factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);

        connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        channel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        channel.Setup(c => c.QueueDeclareAsync(
                options.Value.QueueName,
                true,
                false,
                false,
                null,
                false,
                false,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Queue declare failed"));

        var publisher = new RabbitMqMessagePublisher(options, logger, factory.Object);

        // Act
        var act = async () => await publisher.PublishAsync(new TestMessage("id-1"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Queue declare failed");

        channel.Verify(c => c.BasicPublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}