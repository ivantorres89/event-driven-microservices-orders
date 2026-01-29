using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderAccept.Application.Abstractions;
using OrderAccept.Shared.Correlation;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace OrderAccept.Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher : IMessagePublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RabbitMqMessagePublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqMessagePublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString)
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        // Ensure the queue exists (durable for demo consistency)
        _channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        ).GetAwaiter().GetResult();
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));

        var props = new RabbitMQ.Client.BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = RabbitMQ.Client.DeliveryModes.Persistent
        };

        await _channel.BasicPublishAsync(
            exchange: "",
            routingKey: _options.QueueName,
            mandatory: false,
            basicProperties: props,
            body: body
        );

        _logger.LogInformation("Published message to RabbitMQ queue {QueueName} (Type={MessageType})",
            _options.QueueName, typeof(T).Name);
    }

    public void Dispose()
    {
        try { _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Dispose(); } catch { /* ignore */ }
    }
}
