using Microsoft.Extensions.DependencyInjection;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Handlers;

namespace OrderAccept.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderAcceptApplication(this IServiceCollection services)
    {
        services.AddTransient<IAcceptOrderHandler, AcceptOrderHandler>();
        return services;
    }
}
