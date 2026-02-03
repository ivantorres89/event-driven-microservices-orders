namespace OrderAccept.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// AMQP connection string, e.g. amqp://guest:guest@localhost:5672/
    /// </summary>
    public string ConnectionString { get; init; } = "amqp://guest:guest@localhost:5672/";

    /// <summary>
    /// Outbound queue name for OrderProcessed messages.
    /// </summary>
    public string OutboundQueueName { get; init; } = "order.processed";

    // Backwards-compatible alias (some templates use QueueName).
    public int MaxProcessingAttempts { get; init; } = 5;

    public string QueueName => OutboundQueueName;
}
