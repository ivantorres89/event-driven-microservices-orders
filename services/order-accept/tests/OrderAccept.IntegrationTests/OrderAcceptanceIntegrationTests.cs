using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using FluentAssertions;
using OrderAccept.IntegrationTests.Fixtures;
using OrderAccept.Shared.Correlation;
using OrderAccept.Shared.Workflow;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace OrderAccept.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class OrderAcceptanceIntegrationTests
{
    private readonly OrderAcceptApiFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OrderAcceptanceIntegrationTests(OrderAcceptApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PostOrders_PublishesMessage_And_PersistsWorkflowStateInRedis()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();

        // All endpoints require a real JWT. In tests we mint a dev token using the symmetric key configured for the host.
        var jwt = CreateJwt(_fixture.JwtSigningKey, sub: "customer-it-1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var request = new CreateOrderRequestDto(
            CustomerId: "customer-it-1",
            Items: new[] { new CreateOrderItemDto("AZ-900", 1) });

        // Ensure queue is clean before publishing
        using var connection = await new ConnectionFactory { Uri = new Uri(_fixture.RabbitConnectionString) }
            .CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(_fixture.RabbitQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await channel.QueuePurgeAsync(_fixture.RabbitQueueName);

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", request);

        // Assert: API
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<OrderAcceptedResponse>();
        payload.Should().NotBeNull();
        payload!.CorrelationId.Should().NotBeNullOrWhiteSpace();

        var correlationId = new CorrelationId(Guid.Parse(payload.CorrelationId));

        // Assert: Redis workflow state
        var redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        var db = redis.GetDatabase();

        var redisKey = WorkflowRedisKeys.OrderStatus(correlationId);
        var redisValue = await db.StringGetAsync(redisKey);

        redisValue.HasValue.Should().BeTrue();
        redisValue.ToString().Should().Be("ACCEPTED");

        // Assert: RabbitMQ message published

        // Queue declared by publisher, but we declare too for safety
        await channel.QueueDeclareAsync(_fixture.RabbitQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        // Rabbit publishes are async-ish; poll a few times
        BasicGetResult? result = null;
        for (var i = 0; i < 10 && result is null; i++)
        {
            result = await channel.BasicGetAsync(_fixture.RabbitQueueName, autoAck: true);
            if (result is null)
                await Task.Delay(200);
        }

        result.Should().NotBeNull("a message should be published to RabbitMQ queue");

        var json = Encoding.UTF8.GetString(result!.Body.ToArray());
        var evt = JsonSerializer.Deserialize<OrderAcceptedEventWire>(json, JsonOptions);

        evt.Should().NotBeNull();
        evt!.CorrelationId.Value.Should().Be(correlationId.Value);
        evt.Order.CustomerId.Should().Be(request.CustomerId);
    }

    private sealed record OrderAcceptedResponse(string CorrelationId);

    // Minimal wire shape to validate payload without taking a dependency on Application contracts in IT project
    private sealed record OrderAcceptedEventWire(CorrelationId CorrelationId, CreateOrderRequestDto Order);

    private sealed record CreateOrderRequestDto(string CustomerId, IReadOnlyCollection<CreateOrderItemDto> Items);

    private sealed record CreateOrderItemDto(string ProductId, int Quantity);

    private static string CreateJwt(string signingKey, string sub)
    {
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "order-accept-it",
            audience: "order-accept-it",
            claims: new[] { new Claim("sub", sub) },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


}
