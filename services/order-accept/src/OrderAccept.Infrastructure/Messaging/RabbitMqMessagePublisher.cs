using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OrderAccept.Application.Abstractions;
using RabbitMQ.Client;

namespace OrderAccept.Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher : IMessagePublisher
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly RabbitMqOptions _options;
    private readonly IConnectionFactory _connectionFactory;

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options)
        : this(options, connectionFactory: null)
    {
    }

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options, IConnectionFactory? connectionFactory)
    {
        _options = options.Value;
        _connectionFactory = connectionFactory ?? new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
        };
    }

    public async Task PublishAsync<T>(
        T message, string? routingKey = null, CancellationToken cancellationToken = default)
        where T : class
    {
        // Create a producer span. The activity source is configured in OrderAccept.Shared.Observability.
        using var activity = Observability.ActivitySource.StartActivity(
            name: "rabbitmq.publish",
            kind: ActivityKind.Producer);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", _options.QueueName);
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("messaging.message_type", typeof(T).FullName);

        var correlationId = CorrelationContext.Current?.Value.ToString();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            activity?.SetTag("correlation_id", correlationId);
            // also ensure baggage is present for downstream propagation
            Baggage.SetBaggage("correlation_id", correlationId);
        }

        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Declare queue (idempotent)
        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        // Serialize payload
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        // Propagate trace context + baggage through message headers
        var headers = new Dictionary<string, object>();
        var propagationContext = new PropagationContext(
            activity?.Context ?? Activity.Current?.Context ?? default,
            Baggage.Current);

        Propagator.Inject(propagationContext, headers, static (carrier, key, value) => carrier[key] = value);

        if (!string.IsNullOrWhiteSpace(correlationId))
            headers["x-correlation-id"] = correlationId;

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Headers = headers
        };

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: routingKey ?? _options.QueueName,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);
    }
}
