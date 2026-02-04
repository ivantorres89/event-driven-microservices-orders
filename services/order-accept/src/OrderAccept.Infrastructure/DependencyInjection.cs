using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderAccept.Application.Abstractions;
using OrderAccept.Infrastructure.Messaging;
using OrderAccept.Infrastructure.Workflow;
using OrderAccept.Persistence;
using StackExchange.Redis;

namespace OrderAccept.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderAcceptInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // --- Redis workflow state ---
        services.AddOptions<WorkflowStateOptions>()
            .Bind(configuration.GetSection(WorkflowStateOptions.SectionName));

        var redisConn = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConn))
            throw new InvalidOperationException("ConnectionStrings:Redis is required");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
        services.AddSingleton<IOrderWorkflowStateStore, RedisOrderWorkflowStateStore>();
        services.AddSingleton<IOrderCorrelationMapStore, RedisOrderCorrelationMapStore>();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        // Contoso OLTP persistence (EF Core + SQL Server/Azure SQL)
        services.AddOrderAcceptPersistence(configuration);

        // --- Messaging transport (env-based) ---
        if (environment.IsDevelopment())
        {
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "RabbitMQ:ConnectionString is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.OutboundQueueName), "RabbitMQ:OutboundQueueName is required");

            services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        }
        else
        {
            services.AddOptions<ServiceBusOptions>()
                .Bind(configuration.GetSection(ServiceBusOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "AzureServiceBus:ConnectionString is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.OutboundQueueName), "AzureServiceBus:OutboundQueueName is required");

            services.AddSingleton<IMessagePublisher, ServiceBusMessagePublisher>();
        }

        return services;
    }
}
