using Microsoft.Extensions.Logging;
using OrderProcess.Application.Abstractions;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Contracts.Events;
using OrderProcess.Shared.Workflow;

namespace OrderProcess.Application.Handlers;

public sealed class ProcessOrderHandler : IProcessOrderHandler
{
    private readonly IOrderWorkflowStateStore _workflowState;
    private readonly IOrderOltpWriter _oltpWriter;
    private readonly IMessagePublisher _publisher;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<ProcessOrderHandler> _logger;

    public ProcessOrderHandler(
        IOrderWorkflowStateStore workflowState,
        IOrderOltpWriter oltpWriter,
        IMessagePublisher publisher,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<ProcessOrderHandler> logger)
    {
        _workflowState = workflowState;
        _oltpWriter = oltpWriter;
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

            // 2) OLTP transaction (ToDo real EF+SQL in next iteration)
            var persisted = await _oltpWriter.PersistAsync(command.Event, cancellationToken);
            _logger.LogInformation("Order persisted. Generated OrderId={OrderId}", persisted.OrderId);

            // 3) Redis: set transient workflow state -> COMPLETED + OrderId
            await _workflowState.SetCompletedAsync(command.Event.CorrelationId, persisted.OrderId, cancellationToken);
            _logger.LogInformation("Updated workflow state in Redis: COMPLETED (OrderId={OrderId})", persisted.OrderId);

            // 4) Publish integration event
            var @event = new OrderProcessedEvent(command.Event.CorrelationId, persisted.OrderId);
            await _publisher.PublishAsync(@event, routingKey: null, cancellationToken);
            _logger.LogInformation("Published OrderProcessed integration event");
        }
    }
}
