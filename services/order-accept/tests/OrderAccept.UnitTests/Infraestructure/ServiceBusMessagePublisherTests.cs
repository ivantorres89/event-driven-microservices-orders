using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry;
using OrderAccept.Infrastructure.Messaging;
using OrderAccept.Shared.Correlation;
using OrderAccept.Shared.Resilience;
using Polly.CircuitBreaker;

namespace OrderAccept.UnitTests.Infraestructure;

public sealed class ServiceBusMessagePublisherTests
{
    private sealed record TestMessage(string Id);

    [Fact]
    public async Task PublishAsync_WhenMessageIsValid_SendsWithCorrelationHeader()
    {
        // Arrange
        var options = Options.Create(new ServiceBusOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=not-a-real-key=",
            OutboundQueueName = "order.accepted"
        });

        var logger = Mock.Of<ILogger<ServiceBusMessagePublisher>>();

        var sender = new Mock<IServiceBusSenderAdapter>(MockBehavior.Strict);
        var client = new Mock<IServiceBusClientAdapter>(MockBehavior.Strict);

        ServiceBusMessage? captured = null;

        client.Setup(c => c.CreateSender(options.Value.OutboundQueueName))
            .Returns(sender.Object);

        sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => captured = m)
            .Returns(Task.CompletedTask);

        client.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var correlationId = new CorrelationId(Guid.NewGuid());
        CorrelationContext.Current = correlationId;

        try
        {
            var publisher = new ServiceBusMessagePublisher(options, logger, client.Object);
            var message = new TestMessage("id-1");

            // Act
            await publisher.PublishAsync(message);

            // Assert
            captured.Should().NotBeNull();
            captured!.ContentType.Should().Be("application/json");
            captured.ApplicationProperties.Should().ContainKey("x-correlation-id");
            captured.ApplicationProperties["x-correlation-id"].Should().Be(correlationId.Value.ToString());

            var json = captured.Body.ToString();
            json.Should().Be(JsonSerializer.Serialize(message));
        }
        finally
        {
            CorrelationContext.Current = null;
            Baggage.SetBaggage("correlation_id", null);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenClientCircuitIsOpen_ThrowsDependencyUnavailableException()
    {
        // Arrange
        var options = Options.Create(new ServiceBusOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=not-a-real-key=",
            OutboundQueueName = "order.accepted"
        });

        var logger = Mock.Of<ILogger<ServiceBusMessagePublisher>>();

        var sender = new Mock<IServiceBusSenderAdapter>(MockBehavior.Strict);
        var client = new Mock<IServiceBusClientAdapter>(MockBehavior.Strict);

        client.Setup(c => c.CreateSender(options.Value.OutboundQueueName))
            .Returns(sender.Object);

        sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("circuit open"));

        client.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var publisher = new ServiceBusMessagePublisher(options, logger, client.Object);

        // Act
        var act = async () => await publisher.PublishAsync(new TestMessage("id-1"));

        // Assert
        await act.Should().ThrowAsync<DependencyUnavailableException>()
            .WithMessage("Azure Service Bus is unavailable*");
    }
}
