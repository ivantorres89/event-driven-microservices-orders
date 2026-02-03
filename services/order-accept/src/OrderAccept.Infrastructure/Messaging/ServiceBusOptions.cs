namespace OrderAccept.Infrastructure.Messaging;

public sealed class ServiceBusOptions
{
    public const string SectionName = "AzureServiceBus";

    public string ConnectionString { get; init; } = string.Empty;

    public string OutboundQueueName { get; init; } = "order.accepted";
}
