using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OrderProcess.Infrastructure.Workflow;
using OrderProcess.Shared.Correlation;
using OrderProcess.Shared.Workflow;
using StackExchange.Redis;

namespace OrderProcess.UnitTests;

public sealed class RedisOrderWorkflowStateStoreTests
{
    [Fact]
    public async Task SetStatusAsync_WhenValid_StoresUppercaseStatusWithTtl()
    {
        // Arrange
        var correlationId = new CorrelationId(Guid.NewGuid());
        var options = Options.Create(new WorkflowStateOptions { Ttl = TimeSpan.FromMinutes(5) });
        var logger = Mock.Of<ILogger<RedisOrderWorkflowStateStore>>();

        var db = new Mock<IDatabase>(MockBehavior.Strict);
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);

        RedisKey capturedKey = default;
        RedisValue capturedValue = default;
        TimeSpan? capturedTtl = null;

        db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, ttl, _, _, _) =>
            {
                capturedKey = key;
                capturedValue = value;
                capturedTtl = ttl;
            })
            .ReturnsAsync(true);

        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(db.Object);

        var store = new RedisOrderWorkflowStateStore(redis.Object, options, logger);

        // Act
        await store.SetStatusAsync(correlationId, OrderWorkflowStatus.Accepted);

        // Assert
        capturedKey.ToString().Should().Be(WorkflowRedisKeys.OrderStatus(correlationId));
        capturedValue.ToString().Should().Be("ACCEPTED");
        capturedTtl.Should().Be(options.Value.Ttl);
    }

    [Fact]
    public async Task SetStatusAsync_WhenRedisFails_Throws()
    {
        // Arrange
        var correlationId = new CorrelationId(Guid.NewGuid());
        var options = Options.Create(new WorkflowStateOptions { Ttl = TimeSpan.FromMinutes(5) });
        var logger = Mock.Of<ILogger<RedisOrderWorkflowStateStore>>();

        var db = new Mock<IDatabase>(MockBehavior.Strict);
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);

        db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(db.Object);

        var store = new RedisOrderWorkflowStateStore(redis.Object, options, logger);

        // Act
        var act = async () => await store.SetStatusAsync(correlationId, OrderWorkflowStatus.Accepted);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Redis down");
    }

    [Fact]
    public async Task RemoveStatusAsync_WhenValid_RemovesStatusKey()
    {
        // Arrange
        var correlationId = new CorrelationId(Guid.NewGuid());
        var options = Options.Create(new WorkflowStateOptions());
        var logger = Mock.Of<ILogger<RedisOrderWorkflowStateStore>>();

        var db = new Mock<IDatabase>(MockBehavior.Strict);
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);

        RedisKey capturedKey = default;

        db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => capturedKey = key)
            .ReturnsAsync(true);

        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(db.Object);

        var store = new RedisOrderWorkflowStateStore(redis.Object, options, logger);

        // Act
        await store.RemoveStatusAsync(correlationId);

        // Assert
        capturedKey.ToString().Should().Be(WorkflowRedisKeys.OrderStatus(correlationId));
    }
}
