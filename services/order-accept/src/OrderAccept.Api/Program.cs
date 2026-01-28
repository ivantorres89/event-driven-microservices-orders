using OrderAccept.Api.Endpoints;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Handlers;
using OrderAccept.Infrastructure;
using OrderAccept.Infrastructure.Correlation;

namespace OrderAccept.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();
            builder.Services.AddOpenApi();

            // Add application & infrastructure services
            builder.Services.AddScoped<IAcceptOrderHandler, AcceptOrderHandler>();
            builder.Services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
            builder.Services.AddOrderAcceptInfrastructure(builder.Configuration);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();

            // Map endpoints
            app.MapOrderAcceptEndpoints();

            app.Run();
        }
    }
}
