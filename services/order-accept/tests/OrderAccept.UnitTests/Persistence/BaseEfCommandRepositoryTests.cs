using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Impl.Repositories.Command;

namespace OrderAccept.UnitTests.Persistence;

public sealed class BaseEfCommandRepositoryTests
{
    [Fact]
    public void Add_QueuesEntityAsAdded()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<CustomerCommandRepository>>();
        var repo = new CustomerCommandRepository(db, logger.Object);
        var customer = new Customer { ExternalCustomerId = "c-1" };

        repo.Add(customer);

        db.Entry(customer).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public void Update_QueuesEntityAsModified()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<CustomerCommandRepository>>();
        var repo = new CustomerCommandRepository(db, logger.Object);
        var customer = new Customer { Id = 10, ExternalCustomerId = "c-1" };

        db.Customers.Add(customer);
        db.SaveChanges();

        repo.Update(customer);

        db.Entry(customer).State.Should().Be(EntityState.Modified);
    }

    [Fact]
    public void Delete_QueuesEntityAsDeleted()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<CustomerCommandRepository>>();
        var repo = new CustomerCommandRepository(db, logger.Object);
        var customer = new Customer { Id = 10, ExternalCustomerId = "c-1" };

        db.Customers.Add(customer);
        db.SaveChanges();

        repo.Delete(customer);

        db.Entry(customer).State.Should().Be(EntityState.Deleted);
    }

    [Fact]
    public void Add_WhenEntityIsNull_ThrowsAndLogsError()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var logger = new Mock<ILogger<CustomerCommandRepository>>();
        var repo = new CustomerCommandRepository(db, logger.Object);

        var act = () => repo.Add(null!);

        act.Should().Throw<ArgumentNullException>();
        VerifyLoggedError(logger, "Failed to queue INSERT");
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
}
