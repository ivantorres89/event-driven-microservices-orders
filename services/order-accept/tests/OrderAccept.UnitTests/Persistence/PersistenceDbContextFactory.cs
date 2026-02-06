using Microsoft.EntityFrameworkCore;
using OrderAccept.Persistence.Impl;

namespace OrderAccept.UnitTests.Persistence;

internal static class PersistenceDbContextFactory
{
    public static ContosoDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ContosoDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        return new ContosoDbContext(options);
    }
}
