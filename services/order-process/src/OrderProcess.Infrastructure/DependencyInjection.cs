using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderProcess.Application.Abstractions;
using OrderProcess.Application.Abstractions.Messaging;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Infrastructure.Correlation;
using OrderProcess.Infrastructure.Messaging;
using OrderProcess.Infrastructure.Services;
using OrderProcess.Infrastructure.Workflow;
using StackExchange.Redis;

namespace OrderProcess.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderProcessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // --- Redis (shared workflow state) ---
        services.AddOptions<WorkflowStateOptions>()
            .Bind(configuration.GetSection(WorkflowStateOptions.SectionName));

        var redisConn = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConn))
            throw new InvalidOperationException("ConnectionStrings:Redis is required");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
        services.AddSingleton<IOrderWorkflowStateStore, RedisOrderWorkflowStateStore>();

        // CorrelationIdProvider is scoped (per message scope)
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        // Temporary OLTP writer (next iteration: EF Core + SQL Server/Azure SQL)
        services.AddScoped<IOrderOltpWriter, InMemoryOrderOltpWriter>();

        // --- Messaging transport (env-based) ---
        if (environment.IsDevelopment())
        {
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "RabbitMQ:ConnectionString is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.InboundQueueName), "RabbitMQ:InboundQueueName is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.OutboundQueueName), "RabbitMQ:OutboundQueueName is required");

            services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();

            // Listener hosted service via abstraction
            services.AddSingleton<IOrderAcceptedMessageListener, RabbitMqOrderAcceptedMessageListener>();
            services.AddSingleton<IHostedService>(sp => (IHostedService)sp.GetRequiredService<IOrderAcceptedMessageListener>());
        }
        else
        {
            services.AddOptions<ServiceBusOptions>()
                .Bind(configuration.GetSection(ServiceBusOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "AzureServiceBus:ConnectionString is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.InboundQueueName), "AzureServiceBus:InboundQueueName is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.OutboundQueueName), "AzureServiceBus:OutboundQueueName is required");

            services.AddSingleton<IMessagePublisher, ServiceBusMessagePublisher>();

            services.AddSingleton<IOrderAcceptedMessageListener, ServiceBusOrderAcceptedMessageListener>();
            services.AddSingleton<IHostedService>(sp => (IHostedService)sp.GetRequiredService<IOrderAcceptedMessageListener>());
        }

        return services;
    }
}
