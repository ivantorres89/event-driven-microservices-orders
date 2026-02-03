using Azure.Messaging.ServiceBus;

namespace OrderAccept.Infrastructure.Messaging;

// Internal wrappers to make Service Bus publishing unit-testable without a network connection.
public interface IServiceBusClientAdapter : IAsyncDisposable
{
    IServiceBusSenderAdapter CreateSender(string queueName);
}

public interface IServiceBusSenderAdapter
{
    Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken);
}

internal sealed class ServiceBusClientAdapter : IServiceBusClientAdapter
{
    private readonly ServiceBusClient _client;

    public ServiceBusClientAdapter(ServiceBusClient client)
    {
        _client = client;
    }

    public IServiceBusSenderAdapter CreateSender(string queueName) =>
        new ServiceBusSenderAdapter(_client.CreateSender(queueName));

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}

internal sealed class ServiceBusSenderAdapter : IServiceBusSenderAdapter
{
    private readonly ServiceBusSender _sender;

    public ServiceBusSenderAdapter(ServiceBusSender sender)
    {
        _sender = sender;
    }

    public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken) =>
        _sender.SendMessageAsync(message, cancellationToken);
}
