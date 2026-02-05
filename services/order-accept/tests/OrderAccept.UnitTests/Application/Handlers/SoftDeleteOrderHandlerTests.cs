using FluentAssertions;
using Moq;
using OrderAccept.Application.Abstractions;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Handlers;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Command;
using OrderAccept.Persistence.Abstractions.Repositories.Query;

namespace OrderAccept.UnitTests.Application.Handlers;

public sealed class SoftDeleteOrderHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenInputIsInvalid_ReturnsNotFound()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var handler = new SoftDeleteOrderHandler(uow.Object);

        // Act
        var result = await handler.HandleAsync(0, "");

        // Assert
        result.Should().Be(SoftDeleteOrderOutcome.NotFound);
        uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotOwnOrder_ReturnsForbidden()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var orderQueries = new Mock<IOrderQueryRepository>(MockBehavior.Strict);

        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);
        uow.SetupGet(x => x.OrderQueries).Returns(orderQueries.Object);

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("cust-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer { Id = 1, ExternalCustomerId = "cust-1" });

        orderQueries
            .Setup(q => q.FindAsync(10, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = 10, CustomerId = 2 });

        var handler = new SoftDeleteOrderHandler(uow.Object);

        // Act
        var result = await handler.HandleAsync(10, "cust-1");

        // Assert
        result.Should().Be(SoftDeleteOrderOutcome.Forbidden);
        customerQueries.VerifyAll();
        orderQueries.VerifyAll();
        uow.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_WhenValidRequest_UpdatesOrder_AndReturnsDeleted()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var orderQueries = new Mock<IOrderQueryRepository>(MockBehavior.Strict);
        var orderCommands = new Mock<IOrderCommandRepository>(MockBehavior.Strict);

        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);
        uow.SetupGet(x => x.OrderQueries).Returns(orderQueries.Object);
        uow.SetupGet(x => x.OrderCommands).Returns(orderCommands.Object);

        var order = new Order { Id = 10, CustomerId = 1, IsSoftDeleted = false };

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("cust-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer { Id = 1, ExternalCustomerId = "cust-1" });

        orderQueries
            .Setup(q => q.FindAsync(10, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        orderCommands
            .Setup(c => c.Update(It.Is<Order>(o => o.Id == 10 && o.IsSoftDeleted)));

        uow
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new SoftDeleteOrderHandler(uow.Object);

        // Act
        var result = await handler.HandleAsync(10, "cust-1");

        // Assert
        result.Should().Be(SoftDeleteOrderOutcome.Deleted);
        customerQueries.VerifyAll();
        orderQueries.VerifyAll();
        orderCommands.VerifyAll();
        uow.VerifyAll();
    }
}