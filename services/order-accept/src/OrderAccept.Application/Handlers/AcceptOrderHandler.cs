using Microsoft.Extensions.Logging;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Events;
using OrderAccept.Application.Contracts.Responses;
using OrderAccept.Domain.Entities;
using OrderAccept.Shared.Workflow;

namespace OrderAccept.Application.Handlers;

public sealed class AcceptOrderHandler : IAcceptOrderHandler
{
    private readonly IContosoUnitOfWork _uow;
    private readonly IMessagePublisher _publisher;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IOrderWorkflowStateStore _workflowState;
    private readonly IOrderCorrelationMapStore _correlationMap;
    private readonly ILogger<AcceptOrderHandler> _logger;

    public AcceptOrderHandler(
        IContosoUnitOfWork uow,
        IMessagePublisher publisher,
        IOrderWorkflowStateStore workflowState,
        IOrderCorrelationMapStore correlationMap,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<AcceptOrderHandler> logger)
    {
        _uow = uow;
        _publisher = publisher;
        _correlationIdProvider = correlationIdProvider;
        _workflowState = workflowState;
        _correlationMap = correlationMap;
        _logger = logger;
    }

    public async Task<OrderDto> HandleAsync(
        AcceptOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ExternalCustomerId))
            throw new ArgumentException("ExternalCustomerId is required.", nameof(command));

        var correlationId = _correlationIdProvider.GetCorrelationId();

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId.ToString(),
            ["UserId"] = command.ExternalCustomerId
        }))
        {
            _logger.LogInformation("Creating order for ExternalCustomerId={ExternalCustomerId}", command.ExternalCustomerId);

            // 1) Validate products (contract: 404 if any productId not found / not active).
            var requestedIds = command.Order.Items
                .Select(i => i.ProductId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var products = await _uow.ProductQueries.GetByExternalIdsAsync(requestedIds, asNoTracking: true, cancellationToken);
            var productByExternalId = products.ToDictionary(p => p.ExternalProductId, StringComparer.Ordinal);

            var missing = requestedIds.Where(id => !productByExternalId.ContainsKey(id)).ToArray();
            if (missing.Length > 0)
                throw new ProductNotFoundException(missing);

            // 2) Ensure we have a customer row (demo-friendly: create on first use).
            var customer = await _uow.CustomerQueries.GetByExternalIdAsync(command.ExternalCustomerId, asNoTracking: true, cancellationToken);
            var order = new Order
            {
                CorrelationId = correlationId.Value.ToString()
            };

            if (customer is null)
            {
                var newCustomer = new Customer
                {
                    ExternalCustomerId = command.ExternalCustomerId
                };

                _uow.CustomerCommands.Add(newCustomer);
                order.Customer = newCustomer;
            }
            else
            {
                order.CustomerId = customer.Id;
            }

            // 3) Create order items.
            foreach (var item in command.Order.Items)
            {
                var product = productByExternalId[item.ProductId];
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity
                });
            }

            _uow.OrderCommands.Add(order);

            // 4) Persist.
            await _uow.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created order Id={OrderId}", order.Id);

            // 5) Best-effort: initialize transient workflow state + publish event.
            try
            {
                await _correlationMap.SetUserIdAsync(correlationId, command.ExternalCustomerId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set correlation map (non-blocking)");
            }

            try
            {
                await _workflowState.SetStatusAsync(correlationId, OrderWorkflowStatus.Accepted, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set workflow status (non-blocking)");
            }

            try
            {
                var @event = new OrderAcceptedEvent(
                    correlationId,
                    order.Id,
                    command.ExternalCustomerId,
                    command.Order.Items);

                await _publisher.PublishAsync(@event, null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish OrderAccepted event (non-blocking)");
            }

            // 6) Return contract DTO.
            var dtoItems = command.Order.Items
                .Select(i =>
                {
                    var p = productByExternalId[i.ProductId];
                    return new OrderItemDto(
                        ProductId: p.ExternalProductId,
                        ProductName: p.Name,
                        ImageUrl: p.ImageUrl,
                        UnitPrice: p.Price,
                        Quantity: i.Quantity);
                })
                .ToArray();

            return new OrderDto(
                Id: order.Id,
                CorrelationId: order.CorrelationId,
                CreatedAt: order.CreatedAt,
                Items: dtoItems);
        }
    }
}

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException(IReadOnlyCollection<string> missingExternalIds)
        : base($"One or more products were not found: {string.Join(", ", missingExternalIds)}")
    {
        MissingExternalIds = missingExternalIds;
    }

    public IReadOnlyCollection<string> MissingExternalIds { get; }
}
