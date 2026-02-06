using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.IntegrationTests.Fixtures;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RedisOrderWorkflowStateQueryIntegrationTests
{
    private readonly OrderNotificationLocalInfraFixture _fixture;

    public RedisOrderWorkflowStateQueryIntegrationTests(OrderNotificationLocalInfraFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task GetAsync_WhenCompletedWithOrderId_ParsesOrderId()
    {
        var redis = _fixture.Redis;
        var ttl = _fixture.WorkflowTtl;

        var opts = Options.Create(new WorkflowStateOptions { Ttl = ttl });
        var query = new RedisOrderWorkflowStateQuery(redis, opts, NullLogger<RedisOrderWorkflowStateQuery>.Instance);

        var correlationId = CorrelationId.New();
        var key = WorkflowRedisKeys.OrderStatus(correlationId);

        var db = redis.GetDatabase();
        await db.StringSetAsync(key, "COMPLETED|12345", ttl);

        var state = await query.GetAsync(correlationId);

        state.Should().NotBeNull();
        state!.Status.Should().Be(OrderWorkflowStatus.Completed);
        state.OrderId.Should().Be(12345);
    }

    [Theory]
    [InlineData("ACCEPTED", OrderWorkflowStatus.Accepted)]
    [InlineData("PROCESSING", OrderWorkflowStatus.Processing)]
    [InlineData("COMPLETED", OrderWorkflowStatus.Completed)]
    public async Task GetAsync_WhenKnownStatus_ParsesStatus(string raw, OrderWorkflowStatus expected)
    {
        var redis = _fixture.Redis;
        var ttl = _fixture.WorkflowTtl;

        var opts = Options.Create(new WorkflowStateOptions { Ttl = ttl });
        var query = new RedisOrderWorkflowStateQuery(redis, opts, NullLogger<RedisOrderWorkflowStateQuery>.Instance);

        var correlationId = CorrelationId.New();
        var key = WorkflowRedisKeys.OrderStatus(correlationId);

        var db = redis.GetDatabase();
        await db.StringSetAsync(key, raw, ttl);

        var state = await query.GetAsync(correlationId);

        state.Should().NotBeNull();
        state!.Status.Should().Be(expected);
        state.OrderId.Should().BeNull();
    }
}
