namespace OrderNotification.Infrastructure.Messaging;

public sealed class ServiceBusOptions
{
    public const string SectionName = "AzureServiceBus";

    public string ConnectionString { get; init; } = string.Empty;

    public string InboundQueueName { get; init; } = "order.processed";

    public string OutboundQueueName { get; init; } = "order.processed";
}
