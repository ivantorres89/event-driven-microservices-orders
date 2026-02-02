using Microsoft.Extensions.Logging;
using OrderProcess.Application.Abstractions;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Contracts.Events;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Shared.Workflow;

namespace OrderProcess.Application.Handlers;

public sealed class ProcessOrderHandler : IProcessOrderHandler
{
    private readonly IOrderWorkflowStateStore _workflowState;
    private readonly IContosoUnitOfWork _uow;
    private readonly IMessagePublisher _publisher;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<ProcessOrderHandler> _logger;

    public ProcessOrderHandler(
        IOrderWorkflowStateStore workflowState,
        IContosoUnitOfWork uow,
        IMessagePublisher publisher,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<ProcessOrderHandler> logger)
    {
        _workflowState = workflowState;
        _uow = uow;
        _publisher = publisher;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    public async Task HandleAsync(ProcessOrderCommand command, CancellationToken cancellationToken = default)
    {
        var corr = _correlationIdProvider.GetCorrelationId().ToString();

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = corr
        }))
        {
            _logger.LogInformation("Processing OrderAccepted message for CustomerId={CustomerId}", command.Event.Order.CustomerId);

            // 1) Redis: set transient workflow state -> PROCESSING
            await _workflowState.SetStatusAsync(command.Event.CorrelationId, OrderWorkflowStatus.Processing, cancellationToken);
            _logger.LogInformation("Updated workflow state in Redis: {Status}", OrderWorkflowStatus.Processing);

            // 2) OLTP transaction (Azure SQL / SQL Server)
            //    Defensive idempotency: if the same CorrelationId is processed twice, we reuse the already persisted OrderId.
            var orderId = await PersistOrderAsync(command.Event, cancellationToken);
            _logger.LogInformation("Order persisted (or already existed). OrderId={OrderId}", orderId);

            // 3) Redis: set transient workflow state -> COMPLETED + OrderId
            await _workflowState.SetCompletedAsync(command.Event.CorrelationId, orderId, cancellationToken);
            _logger.LogInformation("Updated workflow state in Redis: COMPLETED (OrderId={OrderId})", orderId);

            // 4) Publish integration event
            var @event = new OrderProcessedEvent(command.Event.CorrelationId, orderId);
            await _publisher.PublishAsync(@event, routingKey: null, cancellationToken);
            _logger.LogInformation("Published OrderProcessed integration event");
        }
    }

    private async Task<long> PersistOrderAsync(OrderAcceptedEvent accepted, CancellationToken cancellationToken)
    {
        var correlationId = accepted.CorrelationId.ToString();

        // Idempotency by CorrelationId (unique key)
        var existing = await _uow.OrderQueries.GetByCorrelationIdAsync(correlationId, cancellationToken);
        if (existing is not null)
            return existing.Id;

        // Upsert Customer by external id
        var customer = await _uow.CustomerQueries.GetByExternalIdAsync(accepted.Order.CustomerId, cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                ExternalCustomerId = accepted.Order.CustomerId,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.invalid",
                PhoneNumber = "+00-000-000-000",
                NationalId = "00000000X",
                DateOfBirth = null,
                AddressLine1 = "",
                City = "",
                PostalCode = "",
                CountryCode = ""
            };
            _uow.CustomerCommands.Add(customer);
        }

        var order = new Order
        {
            CorrelationId = correlationId,
            Customer = customer
        };
        _uow.OrderCommands.Add(order);

        foreach (var i in accepted.Order.Items)
        {
            var product = await _uow.ProductQueries.GetByExternalIdAsync(i.ProductId, cancellationToken);
            if (product is null)
            {
                product = new Product
                {
                    ExternalProductId = i.ProductId,
                    Name = $"Product {i.ProductId}",
                    Category = "Uncategorized",
                    BillingPeriod = "Monthly",
                    IsSubscription = true,
                    Price = 0m,
                    IsActive = true
                };
                _uow.ProductCommands.Add(product);
            }

            _uow.OrderItemCommands.Add(new OrderItem
            {
                Order = order,
                Product = product,
                Quantity = i.Quantity
            });
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return order.Id;
    }
}
