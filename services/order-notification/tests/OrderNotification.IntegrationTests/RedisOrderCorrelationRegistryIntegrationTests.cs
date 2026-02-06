using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderNotification.Infrastructure.Correlation;
using OrderNotification.Infrastructure.Workflow;
using OrderNotification.IntegrationTests.Fixtures;
using OrderNotification.Shared.Correlation;
using OrderNotification.Shared.Workflow;

namespace OrderNotification.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RedisOrderCorrelationRegistryIntegrationTests
{
    private readonly OrderNotificationLocalInfraFixture _fixture;

    public RedisOrderCorrelationRegistryIntegrationTests(OrderNotificationLocalInfraFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task RegisterAsync_Then_ResolveUserIdAsync_ReturnsUserId()
    {
        var redis = _fixture.Redis;
        var ttl = _fixture.WorkflowTtl;
        var opts = Options.Create(new WorkflowStateOptions { Ttl = ttl });
        var registry = new RedisOrderCorrelationRegistry(redis, opts, NullLogger<RedisOrderCorrelationRegistry>.Instance);

        var correlationId = CorrelationId.New();
        var userId = $"user-it-{Guid.NewGuid():N}";
        var key = WorkflowRedisKeys.OrderUserMap(correlationId);

        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(key);

        await registry.RegisterAsync(correlationId, userId);
        var resolved = await registry.ResolveUserIdAsync(correlationId);

        resolved.Should().Be(userId);
    }

    [Fact]
    public async Task ResolveUserIdAsync_WhenLegacyKeyExists_HealsToCanonicalKey()
    {
        var redis = _fixture.Redis;
        var ttl = _fixture.WorkflowTtl;
        var opts = Options.Create(new WorkflowStateOptions { Ttl = ttl });
        var registry = new RedisOrderCorrelationRegistry(redis, opts, NullLogger<RedisOrderCorrelationRegistry>.Instance);

        var correlationId = CorrelationId.New();
        var userId = $"user-it-{Guid.NewGuid():N}";

        var canonicalKey = WorkflowRedisKeys.OrderUserMap(correlationId);
        var legacyKey = $"ws:session:{correlationId}";

        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(canonicalKey);
        await db.KeyDeleteAsync(legacyKey);

        await db.StringSetAsync(legacyKey, userId, ttl);

        var resolved = await registry.ResolveUserIdAsync(correlationId);
        resolved.Should().Be(userId);

        var healed = await db.StringGetAsync(canonicalKey);
        healed.ToString().Should().Be(userId);
    }
}
