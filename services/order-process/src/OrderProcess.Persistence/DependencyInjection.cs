using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Persistence.Impl;

namespace OrderProcess.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderProcessPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlConn = configuration.GetConnectionString("Contoso");
        if (string.IsNullOrWhiteSpace(sqlConn))
            throw new InvalidOperationException("ConnectionStrings:Contoso is required (database name should be 'contoso')");

        services.AddDbContext<ContosoDbContext>(o =>
        {
            o.UseSqlServer(sqlConn, sql =>
            {
                // Keep migrations in this assembly.
                sql.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName);
            });
        });

        // UnitOfWork + repositories
        services.AddScoped<IContosoUnitOfWork, ContosoUnitOfWork>();

        return services;
    }
}
