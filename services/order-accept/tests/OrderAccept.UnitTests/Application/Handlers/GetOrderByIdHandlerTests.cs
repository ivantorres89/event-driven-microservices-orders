using AutoMapper;
using FluentAssertions;
using Moq;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Contracts.Responses;
using OrderAccept.Application.Handlers;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Query;

namespace OrderAccept.UnitTests.Application.Handlers;

public sealed class GetOrderByIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenExternalCustomerIdIsBlank_ReturnsNull()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var handler = new GetOrderByIdHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync(" ", 10);

        // Assert
        result.Should().BeNull();
        mapper.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenOrderIdIsLessThanOne_ReturnsNull()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var handler = new GetOrderByIdHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync("cust-1", 0);

        // Assert
        result.Should().BeNull();
        mapper.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerNotFound_ReturnsNull()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("cust-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var handler = new GetOrderByIdHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync("cust-1", 10);

        // Assert
        result.Should().BeNull();
        customerQueries.VerifyAll();
        mapper.VerifyNoOtherCalls();
        uow.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_WhenOrderNotFound_ReturnsNull()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var orderQueries = new Mock<IOrderQueryRepository>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);
        uow.SetupGet(x => x.OrderQueries).Returns(orderQueries.Object);

        var customer = new Customer { Id = 5, ExternalCustomerId = "cust-1" };

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("cust-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        orderQueries
            .Setup(q => q.FindAsync(10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new GetOrderByIdHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync("cust-1", 10);

        // Assert
        result.Should().BeNull();
        customerQueries.VerifyAll();
        orderQueries.VerifyAll();
        mapper.VerifyNoOtherCalls();
        uow.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_WhenOrderBelongsToDifferentCustomer_ReturnsNull()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var orderQueries = new Mock<IOrderQueryRepository>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);
        uow.SetupGet(x => x.OrderQueries).Returns(orderQueries.Object);

        var customer = new Customer { Id = 5, ExternalCustomerId = "cust-1" };
        var order = new Order { Id = 10, CustomerId = 99 };

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("cust-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        orderQueries
            .Setup(q => q.FindAsync(10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new GetOrderByIdHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync("cust-1", 10);

        // Assert
        result.Should().BeNull();
        customerQueries.VerifyAll();
        orderQueries.VerifyAll();
        mapper.VerifyNoOtherCalls();
        uow.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_WhenValid_ReturnsMappedOrder()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var orderQueries = new Mock<IOrderQueryRepository>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);
        uow.SetupGet(x => x.OrderQueries).Returns(orderQueries.Object);

        var customer = new Customer { Id = 5, ExternalCustomerId = "cust-1" };
        var order = new Order { Id = 10, CustomerId = 5, CorrelationId = "corr-1", CreatedAt = new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc) };

        var mapped = new OrderDto(
            Id: 10,
            CorrelationId: "corr-1",
            CreatedAt: order.CreatedAt,
            Items: Array.Empty<OrderItemDto>());

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("cust-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        orderQueries
            .Setup(q => q.FindAsync(10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        mapper
            .Setup(m => m.Map<OrderDto>(order))
            .Returns(mapped);

        var handler = new GetOrderByIdHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync("cust-1", 10);

        // Assert
        result.Should().BeEquivalentTo(mapped);
        customerQueries.VerifyAll();
        orderQueries.VerifyAll();
        mapper.VerifyAll();
        uow.VerifyAll();
    }
}