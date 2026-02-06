using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrderNotification.Application.Abstractions;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;
using StackExchange.Redis;

namespace OrderNotification.UnitTests.Infraestructure;

public sealed class RedisOrderWorkflowStateQueryTests
{
    [Fact]
    public async Task GetAsync_WhenValueIsCompletedWithOrderId_ReturnsState()
    {
        var correlationId = CorrelationId.New();
        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("COMPLETED|123");

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(db.Object);

        var query = new RedisOrderWorkflowStateQuery(
            redis.Object,
            Options.Create(new WorkflowStateOptions()),
            NullLogger<RedisOrderWorkflowStateQuery>.Instance);

        var result = await query.GetAsync(correlationId);

        result.Should().Be(new OrderWorkflowState(OrderWorkflowStatus.Completed, 123));
    }

    [Fact]
    public async Task GetAsync_WhenValueMissing_ReturnsNull()
    {
        var correlationId = CorrelationId.New();
        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(db.Object);

        var query = new RedisOrderWorkflowStateQuery(
            redis.Object,
            Options.Create(new WorkflowStateOptions()),
            NullLogger<RedisOrderWorkflowStateQuery>.Instance);

        var result = await query.GetAsync(correlationId);

        result.Should().BeNull();
    }
}
