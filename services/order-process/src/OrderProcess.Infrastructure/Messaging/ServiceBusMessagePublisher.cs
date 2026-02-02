using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OrderProcess.Application.Abstractions;
using OrderProcess.Shared.Correlation;
using OrderProcess.Shared.Observability;
using OrderProcess.Shared.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace OrderProcess.Infrastructure.Messaging;

public sealed class ServiceBusMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly ServiceBusOptions _options;
    private readonly IServiceBusClientAdapter _client;
    private readonly ILogger<ServiceBusMessagePublisher> _logger;
    private readonly IAsyncPolicy _publishPolicy;

    public ServiceBusMessagePublisher(IOptions<ServiceBusOptions> options, ILogger<ServiceBusMessagePublisher> logger)
        : this(options, logger, new ServiceBusClientAdapter(new ServiceBusClient(options.Value.ConnectionString)))
    {
    }

    internal ServiceBusMessagePublisher(
        IOptions<ServiceBusOptions> options,
        ILogger<ServiceBusMessagePublisher> logger,
        IServiceBusClientAdapter client)
    {
        _options = options.Value;
        _logger = logger;

        _client = client;

        var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(3), TimeoutStrategy.Optimistic);

        var delays = new[]
        {
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1)
        };

        var retryPolicy = Policy
            .Handle<ServiceBusException>(ex => ex.IsTransient)
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: delays.Length,
                sleepDurationProvider: attempt => delays[attempt - 1],
                onRetry: (ex, sleep, attempt, _) =>
                {
                    _logger.LogWarning(ex,
                        "ServiceBus publish failed (attempt {Attempt}/{Max}). Retrying in {Delay}...",
                        attempt, delays.Length, sleep);
                });

        var breakerPolicy = Policy
            .Handle<ServiceBusException>(ex => ex.IsTransient)
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, breakDelay) => _logger.LogError(ex, "ServiceBus circuit opened for {Delay}", breakDelay),
                onReset: () => _logger.LogInformation("ServiceBus circuit reset"),
                onHalfOpen: () => _logger.LogInformation("ServiceBus circuit half-open"));

        _publishPolicy = Policy.WrapAsync(retryPolicy, breakerPolicy, timeoutPolicy);
    }

    public async Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default) where T : class
    {
        using var activity = Observability.ActivitySource.StartActivity("servicebus.publish", ActivityKind.Producer);

        activity?.SetTag("messaging.system", "azure.servicebus");
        activity?.SetTag("messaging.destination", _options.OutboundQueueName);
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("messaging.message_type", typeof(T).FullName);

        var correlationId = CorrelationContext.Current?.Value.ToString();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            activity?.SetTag("correlation_id", correlationId);
            Baggage.SetBaggage("correlation_id", correlationId);
        }

        try
        {
            await _publishPolicy.ExecuteAsync(async ct =>
            {
                var sender = _client.CreateSender(_options.OutboundQueueName);

                var payload = JsonSerializer.SerializeToUtf8Bytes(message);
                var sbMessage = new ServiceBusMessage(payload)
                {
                    ContentType = "application/json"
                };

                // Trace/baggage propagation
                var headers = new Dictionary<string, string>();
                var propagationContext = new PropagationContext(
                    activity?.Context ?? Activity.Current?.Context ?? default,
                    Baggage.Current);

                Propagator.Inject(propagationContext, headers, static (carrier, key, value) => carrier[key] = value);

                foreach (var kv in headers)
                    sbMessage.ApplicationProperties[kv.Key] = kv.Value;

                if (!string.IsNullOrWhiteSpace(correlationId))
                    sbMessage.ApplicationProperties["x-correlation-id"] = correlationId;

                await sender.SendMessageAsync(sbMessage, ct);
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is ServiceBusException or TimeoutRejectedException or BrokenCircuitException)
        {
            throw new DependencyUnavailableException("Azure Service Bus is unavailable. Failed to publish message.", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
