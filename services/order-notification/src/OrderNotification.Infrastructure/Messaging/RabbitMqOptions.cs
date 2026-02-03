namespace OrderNotification.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// AMQP connection string, e.g. amqp://guest:guest@localhost:5672/
    /// </summary>
    public string ConnectionString { get; init; } = "amqp://guest:guest@localhost:5672/";

    /// <summary>
    /// Inbound queue name for OrderProcessed messages.
    /// </summary>
    public string InboundQueueName { get; init; } = "order.processed";

    /// <summary>
    /// Outbound queue name (unused in order-notification, kept for config parity).
    /// </summary>
    public string OutboundQueueName { get; init; } = "order.processed";

    public int MaxProcessingAttempts { get; init; } = 5;

    // Backwards-compatible alias (some templates use QueueName).
    public string QueueName => OutboundQueueName;
}
