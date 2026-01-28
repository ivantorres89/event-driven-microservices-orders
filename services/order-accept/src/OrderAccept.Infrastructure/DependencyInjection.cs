using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderAccept.Application.Abstractions;
using OrderAccept.Infrastructure.Workflow;
using StackExchange.Redis;

namespace OrderAccept.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderAcceptInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WorkflowStateOptions>(
            configuration.GetSection(WorkflowStateOptions.SectionName));

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnectionString));

            services.AddSingleton<IOrderWorkflowStateStore, RedisOrderWorkflowStateStore>();
        }

        return services;
    }
}
