using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrderProcess.Application.Abstractions;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Contracts.Events;
using OrderProcess.Application.Contracts.Requests;
using OrderProcess.Application.Handlers;
using OrderProcess.IntegrationTests.Fixtures;
using OrderProcess.Persistence;
using OrderProcess.Persistence.Impl;
using OrderProcess.Shared.Correlation;

namespace OrderProcess.IntegrationTests;

/// <summary>
/// OLTP integration tests for ProcessOrderHandler.PersistOrderAsync:
/// - real SQL Server (local docker compose)
/// - real EF Core repositories + UnitOfWork transaction semantics
/// - messaging + redis are stubbed out (not the focus of these tests)
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ProcessOrderHandlerOltpIntegrationTests
{
    private readonly OrderProcessLocalInfraFixture _fixture;

    public ProcessOrderHandlerOltpIntegrationTests(OrderProcessLocalInfraFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HandleAsync_WhenNewOrder_PersistsCustomerProductsOrderAndItems()
    {
        // Arrange
        await using var sp = BuildServiceProvider(_fixture.Configuration);
        await CleanupDatabaseAsync(sp);

        var correlationId = new CorrelationId(Guid.NewGuid());
        await using var handlerScope = sp.CreateAsyncScope();
        var handler = handlerScope.ServiceProvider.GetRequiredService<IProcessOrderHandler>();

        var cmd = CreateCommand(correlationId, customerId: "customer-123", items: new[]
        {
            new CreateOrderItem("product-1", 2),
            new CreateOrderItem("product-2", 1),
        });

        // Act
        await handler.HandleAsync(cmd);

        // Assert
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContosoDbContext>();

        var customers = await db.Customers.ToListAsync();
        var products = await db.Products.ToListAsync();
        var orders = await db.Orders.Include(o => o.Items).ToListAsync();
        var items = await db.OrderItems.ToListAsync();

        customers.Should().HaveCount(1);
        customers[0].ExternalCustomerId.Should().Be("customer-123");

        products.Should().HaveCount(2);
        products.Select(p => p.ExternalProductId).Should().BeEquivalentTo(new[] { "product-1", "product-2" });

        orders.Should().HaveCount(1);
        orders[0].CorrelationId.Should().Be(correlationId.ToString());
        orders[0].Items.Should().HaveCount(2);

        items.Should().HaveCount(2);

        // Publisher is stubbed; verify exactly one publish happened
        var publisher = sp.GetRequiredService<CapturingMessagePublisher>();
        publisher.Published.OfType<OrderProcessedEvent>().Should().HaveCount(1);
        publisher.Published.OfType<OrderProcessedEvent>().Single().CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateCorrelationId_IsIdempotent_DoesNotInsertButPublishesTwice()
    {
        // Arrange
        await using var sp = BuildServiceProvider(_fixture.Configuration);
        await CleanupDatabaseAsync(sp);

        var correlationId = new CorrelationId(Guid.NewGuid());
        await using var handlerScope = sp.CreateAsyncScope();
        var handler = handlerScope.ServiceProvider.GetRequiredService<IProcessOrderHandler>();

        var cmd = CreateCommand(correlationId, customerId: "customer-123", items: new[]
        {
            new CreateOrderItem("product-1", 1),
        });

        // Act
        await handler.HandleAsync(cmd);
        await handler.HandleAsync(cmd);

        // Assert
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContosoDbContext>();

        (await db.Orders.CountAsync()).Should().Be(1);
        (await db.OrderItems.CountAsync()).Should().Be(1);
        (await db.Customers.CountAsync()).Should().Be(1);
        (await db.Products.CountAsync()).Should().Be(1);

        var publisher = sp.GetRequiredService<CapturingMessagePublisher>();
        publisher.Published.OfType<OrderProcessedEvent>().Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_WhenSaveFails_RollsBackTransaction_NoPartialDataIsCommitted()
    {
        // Arrange
        await using var sp = BuildServiceProvider(_fixture.Configuration);
        await CleanupDatabaseAsync(sp);

        var correlationId = new CorrelationId(Guid.NewGuid());
        await using var handlerScope = sp.CreateAsyncScope();
        var handler = handlerScope.ServiceProvider.GetRequiredService<IProcessOrderHandler>();

        // ExternalProductId max len is 64 -> force a SQL truncation error
        var tooLongProductId = new string('X', 100);

        var cmd = CreateCommand(correlationId, customerId: "customer-123", items: new[]
        {
            new CreateOrderItem(tooLongProductId, 1),
        });

        // Act
        var act = async () => await handler.HandleAsync(cmd);

        // Assert
        await act.Should().ThrowAsync<Exception>();

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContosoDbContext>();

        (await db.Orders.CountAsync()).Should().Be(0);
        (await db.OrderItems.CountAsync()).Should().Be(0);
        (await db.Customers.CountAsync()).Should().Be(0);
        (await db.Products.CountAsync()).Should().Be(0);

        var publisher = sp.GetRequiredService<CapturingMessagePublisher>();
        publisher.Published.Should().BeEmpty();
    }

    private static ProcessOrderCommand CreateCommand(CorrelationId correlationId, string customerId, IReadOnlyCollection<CreateOrderItem> items)
    {
        var request = new CreateOrderRequest(customerId, items);
        var accepted = new OrderAcceptedEvent(correlationId, request);
        return new ProcessOrderCommand(accepted);
    }

    private static async Task CleanupDatabaseAsync(IServiceProvider rootProvider)
    {
        await using var scope = rootProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContosoDbContext>();

        // Respect FK order: OrderItem -> Order -> Product/Customer
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [OrderItem];");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Order];");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Product];");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Customer];");
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);

        // Persistence (real)
        services.AddOrderProcessPersistence(configuration);

        // Application handler
        services.AddScoped<IProcessOrderHandler, ProcessOrderHandler>();

        // Stubs
        services.AddSingleton<CapturingMessagePublisher>();
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<CapturingMessagePublisher>());
        services.AddScoped<ICorrelationIdProvider>(_ => new FixedCorrelationIdProvider());
        services.AddSingleton<IOrderWorkflowStateStore, NoopWorkflowStateStore>();

        services.AddLogging();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class FixedCorrelationIdProvider : ICorrelationIdProvider
    {
        public CorrelationId GetCorrelationId() => new(Guid.NewGuid());
    }

    private sealed class NoopWorkflowStateStore : IOrderWorkflowStateStore
    {
        public Task SetStatusAsync(CorrelationId correlationId, Shared.Workflow.OrderWorkflowStatus status, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> TrySetStatusIfExistsAsync(CorrelationId correlationId, Shared.Workflow.OrderWorkflowStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task SetCompletedAsync(CorrelationId correlationId, long orderId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> TrySetCompletedIfExistsAsync(CorrelationId correlationId, long orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task RemoveStatusAsync(CorrelationId correlationId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    public sealed class CapturingMessagePublisher : IMessagePublisher
    {
        public List<object> Published { get; } = new();

        public Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default) where T : class
        {
            Published.Add(message!);
            return Task.CompletedTask;
        }
    }
}
