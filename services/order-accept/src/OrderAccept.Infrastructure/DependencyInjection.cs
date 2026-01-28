using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Handlers;
using OrderAccept.Infrastructure.Workflow;
using StackExchange.Redis;

namespace OrderAccept.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderAcceptInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");
        
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException("Redis connection string is required.");
        }

        // Register Redis connection as singleton
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisConnectionString));

        // Register store as singleton (stateless, thread-safe)
        services.AddSingleton<IOrderWorkflowStateStore, RedisOrderWorkflowStateStore>();

        //ToDo@IVAN: Replace with actual implementation
        //services.AddSingleton<IMessagePublisher, ConcreteMessagePublisher>();
        services.AddSingleton<IAcceptOrderHandler, AcceptOrderHandler>();

        // Configure options
        services.Configure<WorkflowStateOptions>(
            configuration.GetSection(WorkflowStateOptions.SectionName));

        // ToDo@IVAN: Replace with actual implementation
        //services.AddScoped<IMessagePublisher, >();

        return services;
    }
}
