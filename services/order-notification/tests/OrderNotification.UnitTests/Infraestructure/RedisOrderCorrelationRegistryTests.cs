using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrderNotification.Infrastructure.Correlation;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.Shared.Correlation;
using StackExchange.Redis;

namespace OrderNotification.UnitTests.Infraestructure;

public sealed class RedisOrderCorrelationRegistryTests
{
    [Fact]
    public async Task RegisterAsync_WhenUserIdMissing_Throws()
    {
        var registry = new RedisOrderCorrelationRegistry(
            new Mock<IConnectionMultiplexer>().Object,
            Options.Create(new WorkflowStateOptions()),
            NullLogger<RedisOrderCorrelationRegistry>.Instance);

        var act = () => registry.RegisterAsync(CorrelationId.New(), " ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveUserIdAsync_WhenKeyExists_ReturnsValue()
    {
        var correlationId = CorrelationId.New();
        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("user-1");

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(db.Object);

        var registry = new RedisOrderCorrelationRegistry(
            redis.Object,
            Options.Create(new WorkflowStateOptions { Ttl = TimeSpan.FromMinutes(5) }),
            NullLogger<RedisOrderCorrelationRegistry>.Instance);

        var result = await registry.ResolveUserIdAsync(correlationId);

        result.Should().Be("user-1");
    }
}
