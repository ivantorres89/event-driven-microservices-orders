using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Impl.Repositories.Query;

namespace OrderAccept.UnitTests.Persistence;

public sealed class CustomerQueryRepositoryTests
{
    [Fact]
    public async Task GetByExternalIdAsync_IgnoresSoftDeleted()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<CustomerQueryRepository>>();
        var repo = new CustomerQueryRepository(db, logger.Object);

        db.Customers.AddRange(
            new Customer { Id = 1, ExternalCustomerId = "c-1", IsSoftDeleted = false },
            new Customer { Id = 2, ExternalCustomerId = "c-2", IsSoftDeleted = true });
        await db.SaveChangesAsync();

        var result = await repo.GetByExternalIdAsync("c-2");

        result.Should().BeNull();
    }
}
