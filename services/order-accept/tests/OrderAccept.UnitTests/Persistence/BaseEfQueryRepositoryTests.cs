using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Impl;
using OrderAccept.Persistence.Impl.Repositories.Base;

namespace OrderAccept.UnitTests.Persistence;

public sealed class BaseEfQueryRepositoryTests
{
    [Fact]
    public async Task FindAsync_WhenEntityExists_ReturnsEntity()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<TestQueryRepository>>();
        var repo = new TestQueryRepository(db, logger.Object);
        var customer = new Customer { Id = 42, ExternalCustomerId = "c-42" };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var result = await repo.FindAsync(42);

        result.Should().NotBeNull();
        result!.ExternalCustomerId.Should().Be("c-42");
    }

    [Fact]
    public async Task CountAsync_ReturnsCount()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<TestQueryRepository>>();
        var repo = new TestQueryRepository(db, logger.Object);

        db.Customers.AddRange(
            new Customer { Id = 1, ExternalCustomerId = "c-1" },
            new Customer { Id = 2, ExternalCustomerId = "c-2" });
        await db.SaveChangesAsync();

        var count = await repo.CountAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WhenPredicateIsNull_ThrowsAndLogsError()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<TestQueryRepository>>();
        var repo = new TestQueryRepository(db, logger.Object);

        var act = async () => await repo.FirstOrDefaultAsync((Expression<Func<Customer, bool>>)null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
        VerifyLoggedError(logger, "Failed to query");
    }

    private static void VerifyLoggedError<T>(Mock<ILogger<T>> logger, string message)
    {
        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public sealed class TestQueryRepository : BaseEfQueryRepository<Customer>
    {
        public TestQueryRepository(ContosoDbContext db, ILogger<TestQueryRepository> logger)
            : base(db, logger)
        {
        }
    }
}
