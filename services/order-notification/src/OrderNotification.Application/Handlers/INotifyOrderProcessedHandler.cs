using OrderNotification.Application.Commands;

namespace OrderNotification.Application.Handlers;

public interface INotifyOrderProcessedHandler
{
    Task HandleAsync(NotifyOrderProcessedCommand command, CancellationToken cancellationToken = default);
}
