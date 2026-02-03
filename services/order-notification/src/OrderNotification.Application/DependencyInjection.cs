using Microsoft.Extensions.DependencyInjection;
using OrderNotification.Application.Handlers;

namespace OrderNotification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderNotificationApplication(this IServiceCollection services)
    {
        services.AddScoped<INotifyOrderProcessedHandler, NotifyOrderProcessedHandler>();
        return services;
    }
}
