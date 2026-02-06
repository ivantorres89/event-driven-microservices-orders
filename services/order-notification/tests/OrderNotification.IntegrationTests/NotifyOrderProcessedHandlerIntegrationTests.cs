using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderNotification.Application.Abstractions;
using OrderNotification.Application.Commands;
using OrderNotification.Application.Contracts.Events;
using OrderNotification.Application.Exceptions;
using OrderNotification.Application.Handlers;
using OrderNotification.Infrastructure.Correlation;
using OrderNotification.Infrastructure.Services;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.IntegrationTests.Fixtures;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class NotifyOrderProcessedHandlerIntegrationTests
{
    private readonly OrderNotificationLocalInfraFixture _fixture;

    public NotifyOrderProcessedHandlerIntegrationTests(OrderNotificationLocalInfraFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task HandleAsync_WhenMappingExists_NotifiesUser()
    {
        var redis = _fixture.Redis;
        var ttl = _fixture.WorkflowTtl;
        var opts = Options.Create(new WorkflowStateOptions { Ttl = ttl });

        var registry = new RedisOrderCorrelationRegistry(redis, opts, NullLogger<RedisOrderCorrelationRegistry>.Instance);
        var notifier = new CapturingNotifier();
        var correlationProvider = new CorrelationIdProvider();

        var handler = new NotifyOrderProcessedHandler(
            registry,
            notifier,
            correlationProvider,
            NullLogger<NotifyOrderProcessedHandler>.Instance);

        var correlationId = CorrelationId.New();
        var userId = $"user-it-{Guid.NewGuid():N}";

        await registry.RegisterAsync(correlationId, userId);

        CorrelationContext.Current = correlationId;
        try
        {
            var evt = new OrderProcessedEvent(correlationId, OrderId: 9876);
            await handler.HandleAsync(new NotifyOrderProcessedCommand(evt));

            var call = await notifier.Called.Task.WaitAsync(TimeSpan.FromSeconds(10));
            call.UserId.Should().Be(userId);
            call.CorrelationId.Should().Be(correlationId);
            call.Status.Should().Be(OrderWorkflowStatus.Completed);
            call.OrderId.Should().Be(9876);
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    [Fact]
    public async Task HandleAsync_WhenMappingMissing_ThrowsCorrelationMappingNotFoundException()
    {
        var redis = _fixture.Redis;
        var ttl = _fixture.WorkflowTtl;
        var opts = Options.Create(new WorkflowStateOptions { Ttl = ttl });

        var registry = new RedisOrderCorrelationRegistry(redis, opts, NullLogger<RedisOrderCorrelationRegistry>.Instance);
        var notifier = new CapturingNotifier();
        var correlationProvider = new CorrelationIdProvider();

        var handler = new NotifyOrderProcessedHandler(
            registry,
            notifier,
            correlationProvider,
            NullLogger<NotifyOrderProcessedHandler>.Instance);

        var correlationId = CorrelationId.New();

        // Ensure mapping doesn't exist
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(WorkflowRedisKeys.OrderUserMap(correlationId));

        CorrelationContext.Current = correlationId;
        try
        {
            var evt = new OrderProcessedEvent(correlationId, OrderId: 123);

            var act = async () => await handler.HandleAsync(new NotifyOrderProcessedCommand(evt));
            await act.Should().ThrowAsync<CorrelationMappingNotFoundException>();

            notifier.Called.Task.IsCompleted.Should().BeFalse("no notification should be sent if routing cannot be resolved");
        }
        finally
        {
            CorrelationContext.Current = null;
        }
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
