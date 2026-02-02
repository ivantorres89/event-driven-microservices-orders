using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderProcess.Infrastructure.Workflow;
using OrderProcess.IntegrationTests.Fixtures;
using OrderProcess.Shared.Correlation;
using OrderProcess.Shared.Workflow;
using StackExchange.Redis;

namespace OrderProcess.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RedisOrderWorkflowStateStoreIntegrationTests
{
    private readonly OrderProcessLocalInfraFixture _fixture;

    public RedisOrderWorkflowStateStoreIntegrationTests(OrderProcessLocalInfraFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task RedisStore_SetsAndRemovesWorkflowState()
    {
        // Arrange
        var correlationId = CorrelationId.New();

        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        var db = redis.GetDatabase();

        var options = Options.Create(new WorkflowStateOptions { Ttl = TimeSpan.FromMinutes(5) });
        var store = new RedisOrderWorkflowStateStore(redis, options, NullLogger<RedisOrderWorkflowStateStore>.Instance);

        var key = WorkflowRedisKeys.OrderStatus(correlationId);
        await db.KeyDeleteAsync(key);

        // Act + Assert: SetStatus
        await store.SetStatusAsync(correlationId, OrderWorkflowStatus.Processing);
        var v1 = await db.StringGetAsync(key);
        v1.HasValue.Should().BeTrue();
        v1.ToString().Should().Be("PROCESSING");

        var ttl1 = await db.KeyTimeToLiveAsync(key);
        ttl1.Should().NotBeNull();
        ttl1!.Value.Should().BeGreaterThan(TimeSpan.Zero);

        // Act + Assert: SetCompleted
        await store.SetCompletedAsync(correlationId, orderId: 42);
        var v2 = await db.StringGetAsync(key);
        v2.HasValue.Should().BeTrue();
        v2.ToString().Should().Be("COMPLETED|42");

        // Act + Assert: Remove
        await store.RemoveStatusAsync(correlationId);
        var v3 = await db.StringGetAsync(key);
        v3.HasValue.Should().BeFalse();

        // Cleanup
        await db.KeyDeleteAsync(key);
        await redis.CloseAsync();
    }
}
