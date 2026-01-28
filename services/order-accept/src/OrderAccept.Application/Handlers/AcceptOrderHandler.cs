using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Events;
using OrderAccept.Shared.Correlation;
using OrderAccept.Shared.Workflow;

namespace OrderAccept.Application.Handlers
{
    public sealed class AcceptOrderHandler
    {
        private readonly IMessagePublisher _publisher;
        private readonly IOrderWorkflowStateStore _workflowState;

        public AcceptOrderHandler(
            IMessagePublisher publisher,
            IOrderWorkflowStateStore workflowState)
        {
            _publisher = publisher;
            _workflowState = workflowState;
        }

        public async Task HandleAsync(
            AcceptOrderCommand command,
            CancellationToken cancellationToken = default)
        {
            var correlationId = CorrelationId.New();

            // Initialize transient workflow state first.
            // If publishing fails, we compensate by removing the key.
            await _workflowState.SetStatusAsync(
                correlationId,
                OrderWorkflowStatus.Accepted,
                cancellationToken);

            var @event = new OrderAcceptedEvent(
                correlationId,
                command.Order
            );

            try
            {
                await _publisher.PublishAsync(@event, cancellationToken);
            }
            catch
            {
                await _workflowState.RemoveStatusAsync(correlationId, cancellationToken);
                throw;
            }
        }
    }
}
