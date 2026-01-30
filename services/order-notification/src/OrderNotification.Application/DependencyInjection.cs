using Microsoft.Extensions.DependencyInjection;

namespace OrderNotification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderNotificationApplication(this IServiceCollection services)
    {
        return services;
    }
}
