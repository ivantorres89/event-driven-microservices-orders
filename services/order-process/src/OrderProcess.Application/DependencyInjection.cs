using Microsoft.Extensions.DependencyInjection;

namespace OrderProcess.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderProcessApplication(this IServiceCollection services)
    {
        return services;
    }
}
