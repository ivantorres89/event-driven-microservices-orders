using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Events;
using OrderAccept.Shared.Workflow;

namespace OrderAccept.Application.Handlers
{
    public sealed class AcceptOrderHandler : IAcceptOrderHandler
    {
        private readonly IMessagePublisher _publisher;
        private readonly IOrderWorkflowStateStore _workflowState;

        public AcceptOrderHandler(
            IMessagePublisher publisher,
            IOrderWorkflowStateStore workflowState)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _workflowState = workflowState ?? throw new ArgumentNullException(nameof(workflowState));
        }

        public async Task HandleAsync(
            AcceptOrderCommand command,
            CancellationToken cancellationToken = default)
        {
            var correlationId = command.CorrelationId;

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
