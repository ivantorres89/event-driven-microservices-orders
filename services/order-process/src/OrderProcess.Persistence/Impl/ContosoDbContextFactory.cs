using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderProcess.Persistence.Impl;

/// <summary>
/// Design-time factory for EF Core tooling (migrations).
/// </summary>
public sealed class ContosoDbContextFactory : IDesignTimeDbContextFactory<ContosoDbContext>
{
    public ContosoDbContext CreateDbContext(string[] args)
    {
        // Minimal default for tooling; override using --connection.
        var builder = new DbContextOptionsBuilder<ContosoDbContext>();
        builder.UseSqlServer("Server=localhost;Database=contoso;Trusted_Connection=True;TrustServerCertificate=True");
        return new ContosoDbContext(builder.Options);
    }
}
