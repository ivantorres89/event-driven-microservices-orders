using Microsoft.Extensions.DependencyInjection;
using OrderProcess.Application.Handlers;

namespace OrderProcess.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderProcessApplication(this IServiceCollection services)
    {
        services.AddScoped<IProcessOrderHandler, ProcessOrderHandler>();
        return services;
    }
}
