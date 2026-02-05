using FluentAssertions;
using OrderAccept.Api.Validators;
using OrderAccept.Application.Contracts.Requests;

namespace OrderAccept.UnitTests.API.Validators;

public sealed class CreateOrderRequestValidatorTests
{
    [Fact]
    public void Validate_WhenItemsIsNull_IsInvalid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(CustomerId: "customer-1", Items: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Fact]
    public void Validate_WhenItemsIsEmpty_IsInvalid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(CustomerId: "customer-1", Items: []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Fact]
    public void Validate_WhenItemsContainInvalidItem_IsInvalid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(CustomerId: "customer-1", Items:
        [
            new CreateOrderItem("", 0)
        ]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items[0].ProductId");
        result.Errors.Should().Contain(e => e.PropertyName == "Items[0].Quantity");
    }

    [Fact]
    public void Validate_WhenValid_IsValid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(CustomerId: "customer-1", Items: new[]
        {
            new CreateOrderItem("p-1", 1)
        }));

        result.IsValid.Should().BeTrue();
    }
}