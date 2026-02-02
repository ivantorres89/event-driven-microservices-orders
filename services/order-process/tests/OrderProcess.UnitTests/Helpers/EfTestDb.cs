using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrderProcess.Persistence.Impl;

namespace OrderProcess.UnitTests.Helpers;

internal static class EfTestDb
{
    public static ContosoDbContext Create(string? databaseName = null, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<ContosoDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString());

        if (interceptors is { Length: > 0 })
            builder.AddInterceptors(interceptors);

        return new ContosoDbContext(builder.Options);
    }
}
