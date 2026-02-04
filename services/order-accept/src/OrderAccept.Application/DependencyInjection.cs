using Microsoft.Extensions.DependencyInjection;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Handlers;
using OrderAccept.Application.Mapping;

namespace OrderAccept.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderAcceptApplication(this IServiceCollection services)
    {
        services.AddTransient<IAcceptOrderHandler, AcceptOrderHandler>();
        services.AddTransient<IGetProductsHandler, GetProductsHandler>();
        services.AddTransient<IGetProductByIdHandler, GetProductByIdHandler>();
        services.AddTransient<IGetOrdersHandler, GetOrdersHandler>();
        services.AddTransient<ISoftDeleteOrderHandler, SoftDeleteOrderHandler>();

        // DTO mapping (Application decides what the API can expose).
        services.AddAutoMapper(cfg => cfg.AddProfile<DtoMappingProfile>());
        return services;
    }
}
