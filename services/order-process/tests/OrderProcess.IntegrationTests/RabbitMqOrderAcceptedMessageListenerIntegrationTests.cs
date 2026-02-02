using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Handlers;
using OrderProcess.Infrastructure.Messaging;
using OrderProcess.IntegrationTests.Fixtures;
using RabbitMQ.Client;

namespace OrderProcess.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RabbitMqOrderAcceptedMessageListenerIntegrationTests
{
    private readonly OrderProcessLocalInfraFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RabbitMqOrderAcceptedMessageListenerIntegrationTests(OrderProcessLocalInfraFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task Listener_ConsumesOrderAcceptedMessage_And_InvokesHandler()
    {
        // Arrange
        var inboundQueue = _fixture.RabbitInboundQueueName;

        await using var connection = await new ConnectionFactory { Uri = new Uri(_fixture.RabbitConnectionString) }
            .CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(inboundQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueuePurgeAsync(inboundQueue);

        var handlerSpy = new CapturingProcessOrderHandler();

        var services = new ServiceCollection();
        services.AddSingleton(handlerSpy);
        services.AddScoped<IProcessOrderHandler>(sp => sp.GetRequiredService<CapturingProcessOrderHandler>());

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = _fixture.RabbitConnectionString,
            InboundQueueName = inboundQueue,
            OutboundQueueName = _fixture.RabbitOutboundQueueName,
            MaxProcessingAttempts = 3
        });

        var listener = new RabbitMqOrderAcceptedMessageListener(options, scopeFactory, NullLogger<RabbitMqOrderAcceptedMessageListener>.Instance);

        using var cts = new CancellationTokenSource();
        await listener.StartAsync(cts.Token);

        var correlationGuid = Guid.NewGuid();
        var wire = new OrderAcceptedEventWire(
            CorrelationId: new CorrelationIdWire(correlationGuid),
            Order: new CreateOrderRequestWire(
                CustomerId: "customer-it-1",
                Items: new[] { new CreateOrderItemWire("product-it-1", 2) }));

        var json = JsonSerializer.Serialize(wire, JsonOptions);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        // Act
        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: inboundQueue,
            mandatory: false,
            basicProperties: props,
            body: body);

        var received = await handlerSpy.Called.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        received.Should().NotBeNull();
        received.Event.CorrelationId.Value.Should().Be(correlationGuid);
        received.Event.Order.CustomerId.Should().Be("customer-it-1");
        received.Event.Order.Items.Should().ContainSingle();
        received.Event.Order.Items.Single().ProductId.Should().Be("product-it-1");
        received.Event.Order.Items.Single().Quantity.Should().Be(2);

        // Ensure the message was acked (queue becomes empty)
        BasicGetResult? result = null;
        for (var i = 0; i < 10 && result is null; i++)
        {
            result = await channel.BasicGetAsync(inboundQueue, autoAck: true);
            if (result is null)
                await Task.Delay(150);
        }
        result.Should().BeNull("the listener should ACK and remove the message from the queue");

        // Cleanup
        cts.Cancel();
        await listener.StopAsync(CancellationToken.None);
        provider.Dispose();
    }

    [Fact]
    public async Task Listener_RejectsInvalidPayload_And_DoesNotInvokeHandler()
    {
        // Arrange
        var inboundQueue = _fixture.RabbitInboundQueueName;

        await using var connection = await new ConnectionFactory { Uri = new Uri(_fixture.RabbitConnectionString) }
            .CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(inboundQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueuePurgeAsync(inboundQueue);

        var handlerSpy = new CapturingProcessOrderHandler();

        var services = new ServiceCollection();
        services.AddSingleton(handlerSpy);
        services.AddScoped<IProcessOrderHandler>(sp => sp.GetRequiredService<CapturingProcessOrderHandler>());
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = _fixture.RabbitConnectionString,
            InboundQueueName = inboundQueue,
            OutboundQueueName = _fixture.RabbitOutboundQueueName,
            MaxProcessingAttempts = 3
        });

        var listener = new RabbitMqOrderAcceptedMessageListener(options, scopeFactory, NullLogger<RabbitMqOrderAcceptedMessageListener>.Instance);

        using var cts = new CancellationTokenSource();
        await listener.StartAsync(cts.Token);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        // Act: publish invalid payload (missing required fields)
        var body = Encoding.UTF8.GetBytes("{\"bad\":\"payload\"}");
        await channel.BasicPublishAsync("", inboundQueue, false, props, body);

        // Give the consumer a moment
        await Task.Delay(500);

        // Assert: handler not called
        handlerSpy.Called.Task.IsCompleted.Should().BeFalse();

        // Assert: message was rejected without requeue (queue empty)
        BasicGetResult? result = null;
        for (var i = 0; i < 10 && result is null; i++)
        {
            result = await channel.BasicGetAsync(inboundQueue, autoAck: true);
            if (result is null)
                await Task.Delay(150);
        }
        result.Should().BeNull("invalid payload should be rejected without requeue");

        // Cleanup
        cts.Cancel();
        await listener.StopAsync(CancellationToken.None);
        provider.Dispose();
    }

    private sealed class CapturingProcessOrderHandler : IProcessOrderHandler
    {
        public TaskCompletionSource<OrderProcessCommandWire> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(ProcessOrderCommand command, CancellationToken cancellationToken = default)
        {
            var wire = new OrderProcessCommandWire(command.Event);
            Called.TrySetResult(wire);
            return Task.CompletedTask;
        }
    }

    private sealed record OrderProcessCommandWire(OrderProcess.Application.Contracts.Events.OrderAcceptedEvent Event);

    // Minimal wire shapes (same approach as OrderAccept integration tests).
    private sealed record CorrelationIdWire(Guid Value);

    private sealed record CreateOrderRequestWire(string CustomerId, IReadOnlyCollection<CreateOrderItemWire> Items);

    private sealed record CreateOrderItemWire(string ProductId, int Quantity);

    private sealed record OrderAcceptedEventWire(CorrelationIdWire CorrelationId, CreateOrderRequestWire Order);
}
