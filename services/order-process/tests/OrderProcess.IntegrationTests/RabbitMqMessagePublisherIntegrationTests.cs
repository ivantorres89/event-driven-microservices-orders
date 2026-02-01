using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderProcess.Infrastructure.Messaging;
using OrderProcess.IntegrationTests.Fixtures;
using OrderProcess.Shared.Correlation;
using RabbitMQ.Client;

namespace OrderProcess.IntegrationTests;

public sealed class RabbitMqMessagePublisherIntegrationTests
    : IClassFixture<OrderProcessLocalInfraFixture>
{
    private readonly OrderProcessLocalInfraFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RabbitMqMessagePublisherIntegrationTests(OrderProcessLocalInfraFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task Publisher_PublishesMessageToOutboundQueue_WithCorrelationHeader()
    {
        // Arrange
        var outboundQueue = _fixture.RabbitOutboundQueueName;
        await using var connection = await new ConnectionFactory { Uri = new Uri(_fixture.RabbitConnectionString) }
            .CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(outboundQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueuePurgeAsync(outboundQueue);

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = _fixture.RabbitConnectionString,
            InboundQueueName = _fixture.RabbitInboundQueueName,
            OutboundQueueName = outboundQueue
        });

        var publisher = new RabbitMqMessagePublisher(options, NullLogger<RabbitMqMessagePublisher>.Instance);

        var correlation = CorrelationId.New();
        CorrelationContext.Current = correlation;

        var evt = new OrderProcessedEventWire(new CorrelationIdWire(correlation.Value), 12345);

        try
        {
            // Act
            await publisher.PublishAsync(evt);

            // Assert: message arrives in RabbitMQ
            BasicGetResult? result = null;
            for (var i = 0; i < 10 && result is null; i++)
            {
                result = await channel.BasicGetAsync(outboundQueue, autoAck: true);
                if (result is null)
                    await Task.Delay(200);
            }

            result.Should().NotBeNull("a message should be published to the outbound queue");

            // Assert: payload
            var json = Encoding.UTF8.GetString(result!.Body.ToArray());
            var wire = JsonSerializer.Deserialize<OrderProcessedEventWire>(json, JsonOptions);
            wire.Should().NotBeNull();
            wire!.CorrelationId.Value.Should().Be(correlation.Value);
            wire.OrderId.Should().Be(12345);

            // Assert: correlation header
            result.BasicProperties.Headers.Should().NotBeNull();
            result.BasicProperties.Headers!.TryGetValue("x-correlation-id", out var raw).Should().BeTrue();
            ReadHeaderString(raw).Should().Be(correlation.Value.ToString());
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    private static string? ReadHeaderString(object? raw)
        => raw switch
        {
            null => null,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string s => s,
            _ => raw.ToString()
        };

    // Minimal wire shapes (same approach as OrderAccept integration tests).
    private sealed record CorrelationIdWire(Guid Value);
    private sealed record OrderProcessedEventWire(CorrelationIdWire CorrelationId, long OrderId);
}
