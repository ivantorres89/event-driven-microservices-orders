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
    private readonly ILogger<AcceptOrderHandler> _logger;

    public AcceptOrderHandler(
        IMessagePublisher publisher,
        IOrderWorkflowStateStore workflowState,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<AcceptOrderHandler> logger)
    {
        _publisher = publisher;
        _correlationIdProvider = correlationIdProvider;
        _workflowState = workflowState;
        _logger = logger;
    }

    public async Task HandleAsync(
        AcceptOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = _correlationIdProvider.GetCorrelationId().ToString()
        }))
        {
            _logger.LogInformation("Accepting order request for CustomerId={CustomerId}", command.Order.CustomerId);

            // 1) Initialize transient workflow state for notifications/reconnection
            await _workflowState.SetStatusAsync(
                _correlationIdProvider.GetCorrelationId(), OrderWorkflowStatus.Accepted, cancellationToken);
            _logger.LogInformation("Initialized workflow state in Redis: {Status}", OrderWorkflowStatus.Accepted);

            // 2) Publish integration event
            var @event = new OrderAcceptedEvent(_correlationIdProvider.GetCorrelationId(), command.Order);

            try
            {
                await _publisher.PublishAsync(@event, null, cancellationToken);
                _logger.LogInformation("Published OrderAccepted integration event");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish OrderAccepted event. Rolling back transient workflow state.");
                await _workflowState.RemoveStatusAsync(_correlationIdProvider.GetCorrelationId(), cancellationToken);
                throw;
            }
        }
    }
}
