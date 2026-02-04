using OrderAccept.Application.Commands;
using OrderAccept.Application.Contracts.Responses;

namespace OrderAccept.Application.Abstractions;

/// <summary>
/// Defines a handler for processing accept order commands.
/// </summary>
public interface IAcceptOrderHandler
{
    /// <summary>
    /// Processes the specified accept order command.
    /// </summary>
    Task<OrderDto> HandleAsync(
        AcceptOrderCommand command,
        CancellationToken cancellationToken = default);
}
