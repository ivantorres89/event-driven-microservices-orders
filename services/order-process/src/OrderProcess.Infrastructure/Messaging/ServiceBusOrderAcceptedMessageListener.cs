using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
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
using OrderProcess.Shared.Resilience;

namespace OrderProcess.Infrastructure.Messaging;

public sealed class ServiceBusOrderAcceptedMessageListener : BackgroundService, IOrderAcceptedMessageListener, IAsyncDisposable
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly ServiceBusOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceBusOrderAcceptedMessageListener> _logger;
    private readonly ServiceBusClient _client;
    private ServiceBusProcessor? _processor;
    private readonly JsonSerializerOptions _json;

    public ServiceBusOrderAcceptedMessageListener(
        IOptions<ServiceBusOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<ServiceBusOrderAcceptedMessageListener> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _client = new ServiceBusClient(_options.ConnectionString);
        _json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Service Bus listener for queue {Queue}", _options.InboundQueueName);

        _processor = _client.CreateProcessor(_options.InboundQueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        // Keep alive until cancellation
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var sbMessage = args.Message;

        var actions = new ProcessMessageActions(args, sbMessage);
        await ProcessInboundAsync(
            body: sbMessage.Body.ToString(),
            applicationProperties: sbMessage.ApplicationProperties,
            actions: actions,
            cancellationToken: args.CancellationToken);
    }

    // Test-friendly seam so unit tests can validate the listener behaviour without having to
    // instantiate ProcessMessageEventArgs (which has internal constructors).
    internal async Task ProcessInboundAsync(
        string body,
        IReadOnlyDictionary<string, object> applicationProperties,
        IServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        var parent = Propagator.Extract(default, applicationProperties, ExtractHeaderValues);

        using var activity = Observability.ActivitySource.StartActivity(
            "servicebus.consume",
            ActivityKind.Consumer,
            parent.ActivityContext);

        activity?.SetTag("messaging.system", "azure.servicebus");
        activity?.SetTag("messaging.destination", _options.InboundQueueName);
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.operation", "process");

        try
        {
            var message = JsonSerializer.Deserialize<OrderAcceptedEvent>(body, _json);
            if (message is null)
                throw new JsonException("Deserialized OrderAcceptedEvent is null.");

            // CorrelationId MUST come from payload
            CorrelationContext.Current = message.CorrelationId;

            var correlationValue = message.CorrelationId.Value.ToString();
            activity?.SetTag("correlation_id", correlationValue);
            Baggage.SetBaggage("correlation_id", correlationValue);

            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IProcessOrderHandler>();

            await handler.HandleAsync(new ProcessOrderCommand(message), cancellationToken);

            await actions.CompleteAsync(cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid message payload. Dead-lettering message.");
            await actions.DeadLetterAsync("invalid_payload", ex.Message, cancellationToken);
        }
        catch (DependencyUnavailableException ex)
        {
            _logger.LogWarning(ex, "Critical dependency unavailable. Abandoning message for retry.");
            await actions.AbandonAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled processing error. Abandoning message for retry.");
            await actions.AbandonAsync(cancellationToken);
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus processor error. Entity={EntityPath} ErrorSource={Source}", args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }

    private static IEnumerable<string> ExtractHeaderValues(IReadOnlyDictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var raw) || raw is null)
            return Array.Empty<string>();

        return new[] { raw.ToString() ?? string.Empty };
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null)
        {
            await _processor.DisposeAsync();
        }
        await _client.DisposeAsync();
    }
}

internal interface IServiceBusMessageActions
{
    Task CompleteAsync(CancellationToken cancellationToken);
    Task AbandonAsync(CancellationToken cancellationToken);
    Task DeadLetterAsync(string reason, string description, CancellationToken cancellationToken);
}

internal sealed class ProcessMessageActions : IServiceBusMessageActions
{
    private readonly ProcessMessageEventArgs _args;
    private readonly ServiceBusReceivedMessage _message;

    public ProcessMessageActions(ProcessMessageEventArgs args, ServiceBusReceivedMessage message)
    {
        _args = args;
        _message = message;
    }

    public Task CompleteAsync(CancellationToken cancellationToken) =>
        _args.CompleteMessageAsync(_message, cancellationToken);

    public Task AbandonAsync(CancellationToken cancellationToken) =>
        _args.AbandonMessageAsync(_message, cancellationToken: cancellationToken);

    public Task DeadLetterAsync(string reason, string description, CancellationToken cancellationToken) =>
        _args.DeadLetterMessageAsync(_message, reason, description, cancellationToken);
}
