using AutoMapper;
using FluentAssertions;
using Moq;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Contracts.Responses;
using OrderAccept.Application.Handlers;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Query;

namespace OrderAccept.UnitTests.Application.Handlers;

public sealed class GetOrdersHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenExternalCustomerIdIsBlank_ReturnsEmptyPagedResult()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        var handler = new GetOrdersHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync(" ", 0, 10);

        // Assert
        result.Should().BeEquivalentTo(new PagedResult<OrderDto>(0, 10, 0, Array.Empty<OrderDto>()));
        mapper.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenOrdersExist_ReturnsMappedPagedResult()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var customerQueries = new Mock<ICustomerQueryRepository>(MockBehavior.Strict);
        var orderQueries = new Mock<IOrderQueryRepository>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        uow.SetupGet(x => x.CustomerQueries).Returns(customerQueries.Object);
        uow.SetupGet(x => x.OrderQueries).Returns(orderQueries.Object);

        var customer = new Customer { Id = 5, ExternalCustomerId = "cust-1" };
        var orders = new List<Order> { new() { Id = 10, CustomerId = 5 } };

        var mapped = new List<OrderDto>
        {
            new(
                Id: 10,
                CorrelationId: "corr-1",
                CreatedAt: new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc),
                Items: Array.Empty<OrderItemDto>())
        };

        customerQueries
            .Setup(q => q.GetByExternalIdAsync("cust-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        orderQueries
            .Setup(q => q.CountByCustomerIdAsync(5, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        orderQueries
            .Setup(q => q.GetByCustomerIdPagedAsync(5, 0, 10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        mapper
            .Setup(m => m.Map<IReadOnlyCollection<OrderDto>>(orders))
            .Returns(mapped);

        var handler = new GetOrdersHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync("cust-1", 0, 10);

        // Assert
        result.Should().BeEquivalentTo(new PagedResult<OrderDto>(0, 10, 1, mapped));
        customerQueries.VerifyAll();
        orderQueries.VerifyAll();
        mapper.VerifyAll();
        uow.VerifyAll();
    }
}