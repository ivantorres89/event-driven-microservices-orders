using AutoMapper;
using FluentAssertions;
using Moq;
using OrderAccept.Application.Abstractions.Persistence;
using OrderAccept.Application.Contracts.Responses;
using OrderAccept.Application.Handlers;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories.Query;

namespace OrderAccept.UnitTests.Application.Handlers;

public sealed class GetProductsHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenNoProducts_ReturnsEmptyPagedResult()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var productQueries = new Mock<IProductQueryRepository>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        uow.SetupGet(x => x.ProductQueries).Returns(productQueries.Object);

        productQueries
            .Setup(q => q.CountAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new GetProductsHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync(0, 10);

        // Assert
        result.Should().BeEquivalentTo(new PagedResult<ProductDto>(0, 10, 0, Array.Empty<ProductDto>()));
        productQueries.VerifyAll();
        uow.VerifyAll();
        mapper.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenProductsExist_ReturnsMappedPagedResult()
    {
        // Arrange
        var uow = new Mock<IContosoUnitOfWork>(MockBehavior.Strict);
        var productQueries = new Mock<IProductQueryRepository>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);

        uow.SetupGet(x => x.ProductQueries).Returns(productQueries.Object);

        var products = new List<Product>
        {
            new()
            {
                Id = 1,
                ExternalProductId = "p-1",
                Name = "Product 1",
                Category = "Cat",
                Vendor = "Vendor",
                ImageUrl = "https://img/p1",
                Discount = 0,
                BillingPeriod = "Monthly",
                IsSubscription = true,
                Price = 10m,
                IsActive = true
            }
        };

        var mapped = new List<ProductDto>
        {
            new(
                ExternalProductId: "p-1",
                Name: "Product 1",
                Category: "Cat",
                Vendor: "Vendor",
                ImageUrl: "https://img/p1",
                Discount: 0,
                BillingPeriod: "Monthly",
                IsSubscription: true,
                Price: 10m)
        };

        productQueries
            .Setup(q => q.CountAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        productQueries
            .Setup(q => q.GetPagedAsync(0, 10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        mapper
            .Setup(m => m.Map<IReadOnlyCollection<ProductDto>>(products))
            .Returns(mapped);

        var handler = new GetProductsHandler(uow.Object, mapper.Object);

        // Act
        var result = await handler.HandleAsync(0, 10);

        // Assert
        result.Should().BeEquivalentTo(new PagedResult<ProductDto>(0, 10, 1, mapped));
        productQueries.VerifyAll();
        mapper.VerifyAll();
        uow.VerifyAll();
    }
}