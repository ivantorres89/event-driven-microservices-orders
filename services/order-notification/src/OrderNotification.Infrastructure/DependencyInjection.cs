using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderNotification.Application.Abstractions;
using OrderNotification.Application.Abstractions.Messaging;
using OrderNotification.Infrastructure.Correlation;
using OrderNotification.Infrastructure.Messaging;
using OrderNotification.Infrastructure.Services;
using OrderNotification.Infrastructure.Workflow;
using StackExchange.Redis;

namespace OrderNotification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // --- Redis (shared workflow state + correlation registry) ---
        services.AddOptions<WorkflowStateOptions>()
            .Bind(configuration.GetSection(WorkflowStateOptions.SectionName));

        var redisConn = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConn))
            throw new InvalidOperationException("ConnectionStrings:Redis is required");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
        services.AddSingleton<IOrderWorkflowStateStore, RedisOrderWorkflowStateStore>();
        services.AddSingleton<IOrderWorkflowStateQuery, RedisOrderWorkflowStateQuery>();
        services.AddSingleton<IOrderCorrelationRegistry, RedisOrderCorrelationRegistry>();

        // CorrelationIdProvider is scoped (per message scope)
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        // --- Messaging transport (env-based) ---
        if (environment.IsDevelopment())
        {
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "RabbitMQ:ConnectionString is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.InboundQueueName), "RabbitMQ:InboundQueueName is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.OutboundQueueName), "RabbitMQ:OutboundQueueName is required");

            services.AddSingleton<IOrderProcessedMessageListener, RabbitMqOrderProcessedMessageListener>();
            services.AddHostedService(sp => (RabbitMqOrderProcessedMessageListener)sp.GetRequiredService<IOrderProcessedMessageListener>());
        }
        else
        {
            services.AddOptions<ServiceBusOptions>()
                .Bind(configuration.GetSection(ServiceBusOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "AzureServiceBus:ConnectionString is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.InboundQueueName), "AzureServiceBus:InboundQueueName is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.OutboundQueueName), "AzureServiceBus:OutboundQueueName is required");

            services.AddSingleton<IOrderProcessedMessageListener, ServiceBusOrderProcessedMessageListener>();
            services.AddHostedService(sp => (ServiceBusOrderProcessedMessageListener)sp.GetRequiredService<IOrderProcessedMessageListener>());
        }

        return services;
    }
}
