using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderNotification.Application.Abstractions;
using OrderNotification.Application.Commands;
using OrderNotification.Application.Contracts.Events;
using OrderNotification.Application.Handlers;
using OrderNotification.Infrastructure.Correlation;
using OrderNotification.Infrastructure.Messaging;
using OrderNotification.Infrastructure.Services;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.IntegrationTests.Fixtures;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;
using RabbitMQ.Client;

namespace OrderNotification.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RabbitMqOrderProcessedMessageListenerIntegrationTests
{
    private readonly OrderNotificationLocalInfraFixture _fixture;

    public RabbitMqOrderProcessedMessageListenerIntegrationTests(OrderNotificationLocalInfraFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task Listener_ConsumesOrderProcessedMessage_And_NotifiesUser()
    {
        // Arrange
        var inboundQueue = _fixture.RabbitInboundQueueName;

        await using var connection = await new ConnectionFactory { Uri = new Uri(_fixture.RabbitConnectionString) }
            .CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(inboundQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueuePurgeAsync(inboundQueue);

        var notifierSpy = new CapturingNotifier();

        var services = new ServiceCollection();
        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_fixture.Redis);
        services.AddLogging();

        // Real Redis-backed correlation registry

        services.AddSingleton<IOrderCorrelationRegistry>(sp =>
            new RedisOrderCorrelationRegistry(
                sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(),
                Options.Create(new WorkflowStateOptions { Ttl = _fixture.WorkflowTtl }),
                NullLogger<RedisOrderCorrelationRegistry>.Instance));

        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddSingleton<IOrderStatusNotifier>(notifierSpy);
        services.AddScoped<INotifyOrderProcessedHandler, NotifyOrderProcessedHandler>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = _fixture.RabbitConnectionString,
            InboundQueueName = inboundQueue,
            OutboundQueueName = _fixture.RabbitOutboundQueueName,
            MaxProcessingAttempts = 3
        });

        var listener = new RabbitMqOrderProcessedMessageListener(options, scopeFactory, NullLogger<RabbitMqOrderProcessedMessageListener>.Instance);

        using var cts = new CancellationTokenSource();
        await listener.StartAsync(cts.Token);

        var correlationId = CorrelationId.New();
        var userId = $"user-it-{Guid.NewGuid():N}";

        // CorrelationId -> UserId mapping exists (happy path)
        await _fixture.GetRedisDb().StringSetAsync(WorkflowRedisKeys.OrderUserMap(correlationId), userId, _fixture.WorkflowTtl);

        var evt = new OrderProcessedEvent(correlationId, OrderId: 4242);
        var json = JsonSerializer.Serialize(evt);
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

        var call = await notifierSpy.Called.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        call.UserId.Should().Be(userId);
        call.CorrelationId.Should().Be(correlationId);
        call.Status.Should().Be(OrderWorkflowStatus.Completed);
        call.OrderId.Should().Be(4242);

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
    public async Task Listener_RejectsInvalidPayload_And_DoesNotNotifyUser()
    {
        // Arrange
        var inboundQueue = _fixture.RabbitInboundQueueName;

        await using var connection = await new ConnectionFactory { Uri = new Uri(_fixture.RabbitConnectionString) }
            .CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(inboundQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueuePurgeAsync(inboundQueue);

        var notifierSpy = new CapturingNotifier();

        var services = new ServiceCollection();
        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_fixture.Redis);
        services.AddLogging();

        services.AddSingleton<IOrderCorrelationRegistry>(sp =>

            new RedisOrderCorrelationRegistry(
                sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(),
                Options.Create(new WorkflowStateOptions { Ttl = _fixture.WorkflowTtl }),
                NullLogger<RedisOrderCorrelationRegistry>.Instance));

        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddSingleton<IOrderStatusNotifier>(notifierSpy);
        services.AddScoped<INotifyOrderProcessedHandler, NotifyOrderProcessedHandler>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new RabbitMqOptions
        {
            ConnectionString = _fixture.RabbitConnectionString,
            InboundQueueName = inboundQueue,
            OutboundQueueName = _fixture.RabbitOutboundQueueName,
            MaxProcessingAttempts = 3
        });

        var listener = new RabbitMqOrderProcessedMessageListener(options, scopeFactory, NullLogger<RabbitMqOrderProcessedMessageListener>.Instance);

        using var cts = new CancellationTokenSource();
        await listener.StartAsync(cts.Token);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        // Act: publish invalid payload
        var body = Encoding.UTF8.GetBytes("{\"bad\":\"payload\"}");
        await channel.BasicPublishAsync("", inboundQueue, false, props, body);

        // Give the consumer a moment
        await Task.Delay(500);

        // Assert: notifier not called
        notifierSpy.Called.Task.IsCompleted.Should().BeFalse();

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

    private sealed class CapturingNotifier : IOrderStatusNotifier
    {
        public TaskCompletionSource<Call> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task NotifyStatusChangedAsync(
            string userId,
            CorrelationId correlationId,
            OrderWorkflowStatus status,
            long? orderId,
            CancellationToken cancellationToken = default)
        {
            Called.TrySetResult(new Call(userId, correlationId, status, orderId));
            return Task.CompletedTask;
        }

        public sealed record Call(string UserId, CorrelationId CorrelationId, OrderWorkflowStatus Status, long? OrderId);
    }
}
