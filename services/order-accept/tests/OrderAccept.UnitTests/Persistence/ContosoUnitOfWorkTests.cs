using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Persistence.Abstractions.Repositories.Command;
using OrderAccept.Persistence.Abstractions.Repositories.Query;
using OrderAccept.Persistence.Impl;
using OrderAccept.Persistence.Impl.Transactions;

namespace OrderAccept.UnitTests.Persistence;

public sealed class ContosoUnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_CommitsTransaction()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        db.Customers.Add(new OrderAccept.Domain.Entities.Customer { ExternalCustomerId = "c-1" });

        var tx = new Mock<IDbContextTransaction>();
        var factory = new Mock<IContosoTransactionFactory>();
        factory.Setup(f => f.BeginTransactionAsync(db, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var unitOfWork = CreateUnitOfWork(db, factory.Object);

        var rows = await unitOfWork.SaveChangesAsync();

        rows.Should().BeGreaterOrEqualTo(0);
        factory.Verify(f => f.BeginTransactionAsync(db, It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenTransactionFactoryFails_Throws()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var factory = new Mock<IContosoTransactionFactory>();
        factory.Setup(f => f.BeginTransactionAsync(db, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tx failed"));

        var unitOfWork = CreateUnitOfWork(db, factory.Object);

        var act = async () => await unitOfWork.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RollbackAsync_WhenNoTransaction_DoesNotThrow()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var factory = new Mock<IContosoTransactionFactory>();
        var unitOfWork = CreateUnitOfWork(db, factory.Object);

        var act = async () => await unitOfWork.RollbackAsync();

        await act.Should().NotThrowAsync();
    }

    private static IContosoUnitOfWork CreateUnitOfWork(ContosoDbContext db, IContosoTransactionFactory factory)
    {
        return new ContosoUnitOfWork(
            db,
            factory,
            Mock.Of<ICustomerQueryRepository>(),
            Mock.Of<ICustomerCommandRepository>(),
            Mock.Of<IProductQueryRepository>(),
            Mock.Of<IProductCommandRepository>(),
            Mock.Of<IOrderQueryRepository>(),
            Mock.Of<IOrderCommandRepository>(),
            Mock.Of<IOrderItemQueryRepository>(),
            Mock.Of<IOrderItemCommandRepository>(),
            Mock.Of<ILogger<ContosoUnitOfWork>>());
    }
}
