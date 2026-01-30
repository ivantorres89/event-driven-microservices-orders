using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OrderNotification.Application.Abstractions;
using OrderNotification.Shared.Resilience;
using Polly;
using Polly.Timeout;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

namespace OrderNotification.Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher : IMessagePublisher
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly RabbitMqOptions _options;
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;
    private readonly IAsyncPolicy _publishPolicy;

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqMessagePublisher> logger)
        : this(options, logger, connectionFactory: null)
    {
    }

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqMessagePublisher> logger, IConnectionFactory? connectionFactory)
    {
        _options = options.Value;
        _logger = logger;
        _connectionFactory = connectionFactory ?? new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
        };

        // --- Resilience policy (RabbitMQ publisher) ---
        // - Short timeouts per attempt
        // - 3 retries with backoff (200ms, 500ms, 1s)
        // - If still failing: throw DependencyUnavailableException so API returns 503
        var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(2), TimeoutStrategy.Optimistic);

        var delays = new[]
        {
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1)
        };

        var retryPolicy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<AlreadyClosedException>()
            .Or<OperationInterruptedException>()
            .Or<ConnectFailureException>()
            .Or<TimeoutRejectedException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                retryCount: delays.Length,
                sleepDurationProvider: attempt => delays[attempt - 1],
                onRetry: (ex, sleep, attempt, _) =>
                {
                    _logger.LogWarning(ex,
                        "RabbitMQ publish failed (attempt {Attempt}/{Max}). Retrying in {Delay}...",
                        attempt,
                        delays.Length,
                        sleep);
                });

        _publishPolicy = Policy.WrapAsync(retryPolicy, timeoutPolicy);
    }

    public async Task PublishAsync<T>(
        T message, string? routingKey = null, CancellationToken cancellationToken = default)
        where T : class
    {
        // Create a producer span. The activity source is configured in OrderNotification.Shared.Observability.
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

        try
        {
            await _publishPolicy.ExecuteAsync(async ct =>
            {
                await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

                // Enable publisher confirms (best-effort, reflection-based for compatibility).
                // If enabled, we'll also attempt to wait for confirmation.
                await EnablePublisherConfirmsAsync(channel, ct);

                // Declare queue (idempotent)
                await channel.QueueDeclareAsync(
                    queue: _options.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: ct);

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
                    cancellationToken: ct);

                // Wait for publisher confirms if the client supports it.
                await WaitForPublisherConfirmsAsync(channel, ct);
            }, cancellationToken);
        }
        catch (Exception ex) when (
            ex is BrokerUnreachableException or AlreadyClosedException or OperationInterruptedException or ConnectFailureException
            or TimeoutRejectedException or SocketException or TimeoutException)
        {
            throw new DependencyUnavailableException("RabbitMQ is unavailable. Failed to publish message.", ex);
        }
    }

    private static async Task EnablePublisherConfirmsAsync(IChannel channel, CancellationToken ct)
    {
        // RabbitMQ.Client has evolved between major versions. Use reflection to stay compatible.
        var t = channel.GetType();

        // Prefer async enable when available
        var mAsync = t.GetMethod("ConfirmSelectAsync", new[] { typeof(CancellationToken) })
                     ?? t.GetMethod("ConfirmSelectAsync", Type.EmptyTypes);
        if (mAsync is not null)
        {
            var result = mAsync.GetParameters().Length == 1
                ? mAsync.Invoke(channel, new object?[] { ct })
                : mAsync.Invoke(channel, null);

            await AwaitIfTaskLike(result).ConfigureAwait(false);
            return;
        }

        // Fallback to sync enable
        var mSync = t.GetMethod("ConfirmSelect", Type.EmptyTypes);
        mSync?.Invoke(channel, null);
    }

    private static async Task WaitForPublisherConfirmsAsync(IChannel channel, CancellationToken ct)
    {
        var t = channel.GetType();

        // Try common names across versions
        var candidates = new[]
        {
            new { Name = "WaitForConfirmsAsync", Params = new[] { typeof(CancellationToken) } },
            new { Name = "WaitForConfirmsOrDieAsync", Params = new[] { typeof(CancellationToken) } },
            new { Name = "WaitForConfirmationAsync", Params = new[] { typeof(CancellationToken) } },
            new { Name = "WaitForConfirmationsAsync", Params = new[] { typeof(CancellationToken) } },
            new { Name = "WaitForConfirms", Params = Type.EmptyTypes },
            new { Name = "WaitForConfirmsOrDie", Params = Type.EmptyTypes }
        };

        foreach (var c in candidates)
        {
            var m = t.GetMethod(c.Name, c.Params);
            if (m is null) continue;

            var result = c.Params.Length == 1
                ? m.Invoke(channel, new object?[] { ct })
                : m.Invoke(channel, null);

            // Some APIs return bool indicating success
            if (result is bool b)
            {
                if (!b) throw new Exception("RabbitMQ publisher confirms returned false.");
                return;
            }

            await AwaitIfTaskLike(result).ConfigureAwait(false);
            return;
        }

        // If we get here, the client API doesn't expose an explicit wait method.
        // Many recent clients complete BasicPublishAsync only after server confirmation when confirms are enabled.
    }

    private static async Task AwaitIfTaskLike(object? maybeTask)
    {
        if (maybeTask is null) return;

        // Task
        if (maybeTask is Task task)
        {
            await task.ConfigureAwait(false);
            return;
        }

        // ValueTask / ValueTask<T>
        var t = maybeTask.GetType();
        if (t.FullName == "System.Threading.Tasks.ValueTask")
        {
            var asTask = t.GetMethod("AsTask", Type.EmptyTypes);
            if (asTask?.Invoke(maybeTask, null) is Task vtTask)
                await vtTask.ConfigureAwait(false);
            return;
        }

        if (t.IsGenericType && t.GetGenericTypeDefinition().FullName == "System.Threading.Tasks.ValueTask`1")
        {
            var asTask = t.GetMethod("AsTask", Type.EmptyTypes);
            if (asTask?.Invoke(maybeTask, null) is Task vtTask)
                await vtTask.ConfigureAwait(false);
        }
    }
}
