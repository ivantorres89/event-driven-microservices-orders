using Microsoft.Extensions.Logging;
using OrderProcess.Application.Abstractions;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Contracts.Events;
using OrderProcess.Domain.Entities;
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
            var workflowUpdated = await _workflowState.TrySetStatusIfExistsAsync(command.Event.CorrelationId, OrderWorkflowStatus.Processing, cancellationToken);
            if (!workflowUpdated)
                _logger.LogWarning("Workflow state missing in Redis for CorrelationId={CorrelationId}. Proceeding without updating transient status.", command.Event.CorrelationId);
            _logger.LogInformation("Updated workflow state in Redis: {Status}", OrderWorkflowStatus.Processing);

            // 2) OLTP transaction (Azure SQL / SQL Server)
            //    Defensive idempotency: if the same CorrelationId is processed twice, we reuse the already persisted OrderId.
            var persisted = await PersistOrderAsync(command.Event, cancellationToken);
            _logger.LogInformation("Order persisted (or already existed). OrderId={OrderId} IsNew={IsNew}", persisted.OrderId, persisted.IsNew);

            var orderId = persisted.OrderId;

            // 3) Redis: set transient workflow state -> COMPLETED + OrderId
            var completedUpdated = await _workflowState.TrySetCompletedIfExistsAsync(command.Event.CorrelationId, orderId, cancellationToken);
            if (!completedUpdated)
                _logger.LogWarning("Workflow state missing in Redis for CorrelationId={CorrelationId}. Proceeding without updating transient completion.", command.Event.CorrelationId);
            _logger.LogInformation("Updated workflow state in Redis: COMPLETED (OrderId={OrderId})", orderId);

            // 4) Publish integration event (idempotent)
            //    If the order already existed for this CorrelationId, we avoid publishing again to prevent duplicate downstream notifications.
            if (persisted.IsNew)
            {
                var @event = new OrderProcessedEvent(command.Event.CorrelationId, orderId);
                await _publisher.PublishAsync(@event, routingKey: null, cancellationToken);
                _logger.LogInformation("Published OrderProcessed integration event");
            }
            else
            {
                _logger.LogInformation("Order already existed for CorrelationId={CorrelationId}. Skipping OrderProcessed publish.", command.Event.CorrelationId);
            }
        }
    }

    internal async Task<PersistOrderResult> PersistOrderAsync(OrderAcceptedEvent accepted, CancellationToken cancellationToken)
    {
        var correlationId = accepted.CorrelationId.ToString();

        // Idempotency by CorrelationId (unique key)
        var existing = await _uow.OrderQueries.GetByCorrelationIdAsync(correlationId, cancellationToken);
        if (existing is not null)
        {
            return new PersistOrderResult(existing.Id, IsNew: false);
        }

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
        return new PersistOrderResult(order.Id, IsNew: true);
    }


    internal sealed record PersistOrderResult(long OrderId, bool IsNew);
}
