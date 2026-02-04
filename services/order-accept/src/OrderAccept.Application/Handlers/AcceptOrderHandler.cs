using Microsoft.Extensions.Logging;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Events;
using OrderAccept.Shared.Workflow;

namespace OrderAccept.Application.Handlers;

public sealed class AcceptOrderHandler : IAcceptOrderHandler
{
    private readonly IMessagePublisher _publisher;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IOrderWorkflowStateStore _workflowState;
    private readonly IOrderCorrelationMapStore _correlationMap;
    private readonly ILogger<AcceptOrderHandler> _logger;

    public AcceptOrderHandler(
        IMessagePublisher publisher,
        IOrderWorkflowStateStore workflowState,
        IOrderCorrelationMapStore correlationMap,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<AcceptOrderHandler> logger)
    {
        _publisher = publisher;
        _correlationIdProvider = correlationIdProvider;
        _workflowState = workflowState;
        _correlationMap = correlationMap;
        _logger = logger;
    }

    public async Task HandleAsync(
        AcceptOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId.ToString(),
            ["UserId"] = command.Order.CustomerId
        }))
        {
            _logger.LogInformation("Accepting order request for CustomerId={CustomerId}", command.Order.CustomerId);

            // 1) Persist short-lived CorrelationId -> UserId mapping for downstream notifications
            await _correlationMap.SetUserIdAsync(correlationId, command.Order.CustomerId, cancellationToken);
            _logger.LogInformation("Initialized correlation mapping in Redis");

            // 2) Initialize transient workflow state for notifications/reconnection
            try
            {
                await _workflowState.SetStatusAsync(correlationId, OrderWorkflowStatus.Accepted, cancellationToken);
                _logger.LogInformation("Initialized workflow state in Redis: {Status}", OrderWorkflowStatus.Accepted);
            }
            catch
            {
                // If we can't store status (Redis dependency), best-effort cleanup of mapping to avoid stale keys.
                await _correlationMap.RemoveAsync(correlationId, cancellationToken);
                throw;
            }

            // 3) Publish integration event
            var @event = new OrderAcceptedEvent(correlationId, command.Order);

            try
            {
                await _publisher.PublishAsync(@event, null, cancellationToken);
                _logger.LogInformation("Published OrderAccepted integration event");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish OrderAccepted event. Rolling back transient workflow state.");
                await _workflowState.RemoveStatusAsync(correlationId, cancellationToken);
                await _correlationMap.RemoveAsync(correlationId, cancellationToken);
                throw;
            }
        }
    }
}
