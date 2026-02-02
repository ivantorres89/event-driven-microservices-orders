using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace OrderProcess.DatabaseMigrations;

/// <summary>
/// In-process migration runner used by:
/// - the OrderProcess.DatabaseMigrations console app
/// - integration tests (to validate migrations in this repo)
/// </summary>
public static class ContosoMigrator
{
    public static void MigrateUp(string connectionString, long? targetVersion = null, LogLevel minLogLevel = LogLevel.Information)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));

        var services = new ServiceCollection();

        services.AddLogging(lb => lb
            .AddConsole()
            .SetMinimumLevel(minLogLevel));

        services
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSqlServer()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations().For.EmbeddedResources()
                .WithVersionTable(new ContosoVersionTable()))
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        using var serviceProvider = services.BuildServiceProvider(validateScopes: false);
        using var scope = serviceProvider.CreateScope();

        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

        if (targetVersion is null)
            runner.MigrateUp();
        else
            runner.MigrateUp(targetVersion.Value);
    }
}
