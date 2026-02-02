using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OrderProcess.Application.Abstractions.Messaging;
using OrderProcess.Application.Commands;
using OrderProcess.Application.Contracts.Events;
using OrderProcess.Application.Handlers;
using OrderProcess.Shared.Correlation;
using OrderProcess.Shared.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderProcess.Infrastructure.Messaging;

public sealed class RabbitMqOrderAcceptedMessageListener : BackgroundService, IOrderAcceptedMessageListener
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqOrderAcceptedMessageListener> _logger;
    private readonly IConnectionFactory _connectionFactory;
    private readonly JsonSerializerOptions _json;

    public RabbitMqOrderAcceptedMessageListener(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqOrderAcceptedMessageListener> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            DispatchConsumersAsync = true
        };

        _json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting RabbitMQ listener for queue {Queue}", _options.InboundQueueName);

        await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _options.InboundQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            await HandleMessageAsync(channel, ea, stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: _options.InboundQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Keep the background service alive.
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    internal async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        IDictionary<string, object> headers = ea.BasicProperties.Headers ?? new Dictionary<string, object>();

        // Extract trace context if present
        var parentContext = Propagator.Extract(default, headers, ExtractHeaderValues);

        using var activity = Observability.ActivitySource.StartActivity(
            "rabbitmq.consume",
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", _options.InboundQueueName);
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.operation", "process");

        OrderAcceptedEvent? message = null;

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            message = JsonSerializer.Deserialize<OrderAcceptedEvent>(json, _json);

            if (message is null)
                throw new JsonException("Deserialized OrderAcceptedEvent is null.");

            // CorrelationId MUST come from payload
            CorrelationContext.Current = message.CorrelationId;

            var correlationValue = message.CorrelationId.Value.ToString();
            activity?.SetTag("correlation_id", correlationValue);
            Baggage.SetBaggage("correlation_id", correlationValue);

            // Per-message DI scope
            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IProcessOrderHandler>();

            await handler.HandleAsync(new ProcessOrderCommand(message), ct);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid message payload. Rejecting without requeue.");
            await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Retry policy (dev approximation): republish with x-retry-count header.
            var retryCount = GetRetryCount(headers);
            if (retryCount < _options.MaxProcessingAttempts)
            {
                var next = retryCount + 1;
                _logger.LogWarning(ex, "Message processing failed. Republish for retry {Retry}/{Max}.", next, _options.MaxProcessingAttempts);

                headers["x-retry-count"] = next;

                // Re-publish to same queue (default exchange)
                var props = new BasicProperties
                {
                    Persistent = true,
                    ContentType = ea.BasicProperties.ContentType ?? "application/json",
                    Headers = headers
                };

                // Preserve message body
                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: _options.InboundQueueName,
                    mandatory: false,
                    basicProperties: props,
                    body: ea.Body,
                    cancellationToken: ct);

                // Ack original so we don't get immediate redelivery loops
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            else
            {
                _logger.LogError(ex, "Max processing attempts exceeded. Rejecting without requeue (DLQ via DLX if configured).");
                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, cancellationToken: ct);
            }
        }
        finally
        {
            // Prevent leaking correlation across messages
            CorrelationContext.Current = null;
        }
    }

    private static IEnumerable<string> ExtractHeaderValues(IDictionary<string, object> headers, string key)
    {
        if (!headers.TryGetValue(key, out var raw) || raw is null)
            return Array.Empty<string>();

        return raw switch
        {
            byte[] bytes => new[] { Encoding.UTF8.GetString(bytes) },
            string s => new[] { s },
            _ => new[] { raw.ToString() ?? string.Empty }
        };
    }

    private static int GetRetryCount(IDictionary<string, object> headers)
    {
        if (!headers.TryGetValue("x-retry-count", out var raw) || raw is null)
            return 0;

        try
        {
            return raw switch
            {
                byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var v) => v,
                int i => i,
                long l => (int)l,
                string s when int.TryParse(s, out var v) => v,
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }
}
