using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderNotification.Application.Abstractions;
using OrderNotification.Infrastructure.Messaging;
using OrderNotification.Infrastructure.Workflow;
using StackExchange.Redis;

namespace OrderNotification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "RabbitMQ:ConnectionString is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.QueueName), "RabbitMQ:QueueName is required");

        services.AddOptions<WorkflowStateOptions>()
            .Bind(configuration.GetSection(WorkflowStateOptions.SectionName));

        var redisConn = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConn))
            throw new InvalidOperationException("ConnectionStrings:Redis is required");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        services.AddSingleton<IOrderWorkflowStateStore, RedisOrderWorkflowStateStore>();

        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        return services;
    }
}
