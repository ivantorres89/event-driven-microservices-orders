namespace OrderProcess.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// AMQP connection string, e.g. amqp://guest:guest@localhost:5672/
    /// </summary>
    public string ConnectionString { get; init; } = "amqp://guest:guest@localhost:5672/";

    /// <summary>
    /// Target queue for OrderProcessed messages (FIFO semantics in this demo).
    /// </summary>
    public string QueueName { get; init; } = "order.accepted";
}
