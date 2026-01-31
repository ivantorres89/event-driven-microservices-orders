using OrderProcess.Application.Commands;

namespace OrderProcess.Application.Handlers;

/// <summary>
/// Orchestrates the order processing workflow for a single OrderAccepted message.
/// </summary>
public interface IProcessOrderHandler
{
    Task HandleAsync(ProcessOrderCommand command, CancellationToken cancellationToken = default);
}
