using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;
using StackExchange.Redis;

namespace OrderNotification.UnitTests.Infraestructure;

public sealed class RedisOrderWorkflowStateStoreTests
{
    [Fact]
    public async Task TrySetStatusIfExistsAsync_WhenUpdated_ReturnsTrueAndUsesUppercase()
    {
        var correlationId = CorrelationId.New();
        RedisValue? capturedValue = null;

        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                When.Exists))
            .Callback<RedisKey, RedisValue, TimeSpan?, When>((_, value, _, _) => capturedValue = value)
            .ReturnsAsync(true);

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(db.Object);

        var store = new RedisOrderWorkflowStateStore(
            redis.Object,
            Options.Create(new WorkflowStateOptions()),
            NullLogger<RedisOrderWorkflowStateStore>.Instance);

        var result = await store.TrySetStatusIfExistsAsync(correlationId, OrderWorkflowStatus.Accepted);

        result.Should().BeTrue();
        capturedValue.Should().Be("ACCEPTED");
    }
}
