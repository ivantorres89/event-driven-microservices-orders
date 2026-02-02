using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using OrderProcess.DatabaseMigrations;
using Microsoft.Data.SqlClient;

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

// --- Ensure database exists (local SQL container doesn't auto-create DB) ---
try
{
    var csb = new SqlConnectionStringBuilder(connection);

    var dbName = csb.InitialCatalog;
    if (string.IsNullOrWhiteSpace(dbName))
        throw new InvalidOperationException("Connection string must include a database name (Initial Catalog/Database).");

    // Connect to master to create the target DB if missing
    var masterCsb = new SqlConnectionStringBuilder(connection)
    {
        InitialCatalog = "master"
    };

    await EnsureDatabaseExistsAsync(masterCsb.ConnectionString, dbName);
}
catch (Exception ex)
{
    Console.Error.WriteLine("Failed while ensuring database exists:");
    Console.Error.WriteLine(ex);
    return 1;
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

static async Task EnsureDatabaseExistsAsync(string masterConnectionString, string databaseName)
{
    // Retry loop because SQL Server container takes a bit to accept connections.
    const int maxAttempts = 60;
    var delay = TimeSpan.FromSeconds(2);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await using var conn = new SqlConnection(masterConnectionString);
            await conn.OpenAsync();

            // CREATE DATABASE if missing (idempotent)
            var safeDbNameForLiteral = databaseName.Replace("'", "''");
            var safeDbNameForBracket = databaseName.Replace("]", "]]");

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"IF DB_ID(N'{safeDbNameForLiteral}') IS NULL
BEGIN
    CREATE DATABASE [{safeDbNameForBracket}];
END";
            await cmd.ExecuteNonQueryAsync();

            return;
        }
        catch when (attempt < maxAttempts)
        {
            await Task.Delay(delay);
        }
    }

    // One final attempt to get a useful exception
    await using var finalConn = new SqlConnection(masterConnectionString);
    await finalConn.OpenAsync();
}
