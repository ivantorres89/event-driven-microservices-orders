using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Persistence.Abstractions.Repositories.Command;
using OrderProcess.Persistence.Impl;
using OrderProcess.Persistence.Impl.Transactions;
using OrderProcess.UnitTests.Helpers;

namespace OrderProcess.UnitTests.Persistence;

public sealed class ContosoUnitOfWorkTests
{
    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private static IContosoUnitOfWork CreateUow(
        ContosoDbContext db,
        IContosoTransactionFactory txFactory,
        ILogger<ContosoUnitOfWork>? logger = null)
    {
        // Repositories are not part of the UoW behavior under test, but required by ctor.
        var customerQ = new Mock<ICustomerQueryRepository>(MockBehavior.Strict).Object;
        var customerC = new Mock<ICustomerCommandRepository>(MockBehavior.Strict).Object;
        var productQ = new Mock<IProductQueryRepository>(MockBehavior.Strict).Object;
        var productC = new Mock<IProductCommandRepository>(MockBehavior.Strict).Object;
        var orderQ = new Mock<IOrderQueryRepository>(MockBehavior.Strict).Object;
        var orderC = new Mock<IOrderCommandRepository>(MockBehavior.Strict).Object;
        var itemQ = new Mock<IOrderItemQueryRepository>(MockBehavior.Strict).Object;
        var itemC = new Mock<IOrderItemCommandRepository>(MockBehavior.Strict).Object;

        return new ContosoUnitOfWork(
            db,
            txFactory,
            customerQ,
            customerC,
            productQ,
            productC,
            orderQ,
            orderC,
            itemQ,
            itemC,
            logger ?? Mock.Of<ILogger<ContosoUnitOfWork>>());
    }

    [Fact]
    public async Task SaveChangesAsync_WhenSuccessful_CommitsAndDisposesTransaction()
    {
        // Arrange
        await using var db = EfTestDb.Create();

        db.Products.Add(new Product
        {
            ExternalProductId = "prod-commit",
            Name = "Commit",
            Category = "Billing",
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 1m,
            IsActive = true
        });

        var tx = new Mock<IDbContextTransaction>(MockBehavior.Strict);
        tx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var factory = new Mock<IContosoTransactionFactory>(MockBehavior.Strict);
        factory.Setup(f => f.BeginTransactionAsync(db, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var uow = CreateUow(db, factory.Object);

        // Act
        var rows = await uow.SaveChangesAsync();

        // Assert
        rows.Should().BeGreaterOrEqualTo(1);
        factory.Verify(f => f.BeginTransactionAsync(db, It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenSaveChangesFails_RollsBackAndDisposesTransaction()
    {
        // Arrange
        var interceptor = new ThrowingSaveChangesInterceptor();
        await using var db = EfTestDb.Create(databaseName: Guid.NewGuid().ToString(), interceptor);

        db.Products.Add(new Product
        {
            ExternalProductId = "prod-fail",
            Name = "Fail",
            Category = "Billing",
            BillingPeriod = "Monthly",
            IsSubscription = true,
            Price = 1m,
            IsActive = true
        });

        var tx = new Mock<IDbContextTransaction>(MockBehavior.Strict);
        tx.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var factory = new Mock<IContosoTransactionFactory>(MockBehavior.Strict);
        factory.Setup(f => f.BeginTransactionAsync(db, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        var uow = CreateUow(db, factory.Object);

        // Act
        var act = async () => await uow.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        tx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task RollbackAsync_WhenNoTransaction_DoesNothing()
    {
        // Arrange
        await using var db = EfTestDb.Create();
        var factory = Mock.Of<IContosoTransactionFactory>();
        var uow = CreateUow(db, factory);

        // Act
        await uow.RollbackAsync();

        // Assert
        // No exception.
    }
}
