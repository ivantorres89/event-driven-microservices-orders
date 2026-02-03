namespace OrderAccept.Application.Abstractions;

/// <summary>
/// Defines a contract for asynchronously publishing messages of a specified type to a message bus or transport.
/// </summary>
/// <remarks>Implementations may deliver messages to various back-end systems, such as message queues, event
/// streams, or other messaging infrastructures. Thread safety and delivery guarantees depend on the specific
/// implementation.</remarks>
public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default)
        where T : class;
}
