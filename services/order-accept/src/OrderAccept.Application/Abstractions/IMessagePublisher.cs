namespace OrderAccept.Application.Abstractions
{
    public interface IMessagePublisher
    {
        /// <summary>
        /// Publishes a message asynchronously to the underlying message bus or transport.
        /// </summary>
        /// <typeparam name="T">The type of the message to publish. Must be a reference type.</typeparam>
        /// <param name="message">The message instance to publish. Cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the publish operation.</param>
        /// <returns>A task that represents the asynchronous publish operation.</returns>
        Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
            where T : class;
    }
}
