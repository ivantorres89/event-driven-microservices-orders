using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Events;
using OrderAccept.Shared.Correlation;

namespace OrderAccept.Application.Handlers
{
    public sealed class AcceptOrderHandler
    {
        private readonly IMessagePublisher _publisher;

        public AcceptOrderHandler(IMessagePublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task HandleAsync(
            AcceptOrderCommand command,
            CancellationToken cancellationToken = default)
        {
            var correlationId = CorrelationId.New();

            var @event = new OrderAcceptedEvent(
                correlationId,
                command.Order
            );

            await _publisher.PublishAsync(@event, cancellationToken);
        }
    }
}
