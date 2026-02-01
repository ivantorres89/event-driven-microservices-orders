using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

// Usage examples:
//  dotnet run --project src/OrderProcess.DatabaseMigrations -- --connection "<conn>" --to 2
//  dotnet run --project src/OrderProcess.DatabaseMigrations -- --to 1  (connection from ConnectionStrings:Contoso/env)

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

// Command line supports: --connection "..."  OR  connection="..."
var connection = configuration["connection"]
                 ?? configuration.GetConnectionString("Contoso")
                 ?? Environment.GetEnvironmentVariable("CONTOSO_CONNECTIONSTRING");

if (string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("Missing connection string. Provide --connection or set ConnectionStrings:Contoso.");
    return 2;
}

long? targetVersion = null;
if (long.TryParse(configuration["to"], out var v))
    targetVersion = v;

var services = new ServiceCollection();
services.AddLogging(lb => lb
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));

services
    .AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddSqlServer()
        .WithGlobalConnectionString(connection)
        .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations()
        .WithVersionTable(new ContosoVersionTable()))
    .AddLogging(lb => lb.AddFluentMigratorConsole());

using var serviceProvider = services.BuildServiceProvider(validateScopes: false);
using var scope = serviceProvider.CreateScope();

var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

try
{
    if (targetVersion is null)
    {
        runner.MigrateUp();
    }
    else
    {
        runner.MigrateUp(targetVersion.Value);
    }

    Console.WriteLine(targetVersion is null
        ? "Migrations applied successfully."
        : $"Migrations applied successfully up to version {targetVersion.Value}.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    return 1;
}
