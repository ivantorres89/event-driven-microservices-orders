using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderAccept.Persistence.Impl;

/// <summary>
/// Design-time factory for EF Core tooling (migrations).
/// </summary>
public sealed class ContosoDbContextFactory : IDesignTimeDbContextFactory<ContosoDbContext>
{
    public ContosoDbContext CreateDbContext(string[] args)
    {
        // Minimal default for tooling; override using --connection.
        var builder = new DbContextOptionsBuilder<ContosoDbContext>();
        builder.UseSqlServer("Server=localhost,1433;Database=contoso;User ID=sa;Password=Your_strong_Password123!;TrustServerCertificate=True;Encrypt=False");
        return new ContosoDbContext(builder.Options);
    }
}
