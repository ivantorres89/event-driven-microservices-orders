using OrderAccept.Application.Commands;

namespace OrderAccept.Application.Abstractions
{
    /// <summary>
    /// Defines a handler for processing accept order commands asynchronously.
    /// </summary>
    public interface IAcceptOrderHandler
    {
        /// <summary>
        /// Processes the specified accept order command asynchronously.
        /// </summary>
        /// <param name="command">The command containing the details required to accept an order. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task HandleAsync(
            AcceptOrderCommand command,
            CancellationToken cancellationToken = default);
    }
}