using FluentAssertions;
using OrderAccept.Api.Validators;
using OrderAccept.Application.Contracts.Requests;

namespace OrderAccept.UnitTests.API.Validators;

public sealed class CreateOrderItemValidatorTests
{
    [Fact]
    public void Validate_WhenProductIdIsEmpty_IsInvalid()
    {
        var validator = new CreateOrderItemValidator();
        var result = validator.Validate(new CreateOrderItem("", 1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProductId");
    }

    [Fact]
    public void Validate_WhenQuantityIsZero_IsInvalid()
    {
        var validator = new CreateOrderItemValidator();
        var result = validator.Validate(new CreateOrderItem("p-1", 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }

    [Fact]
    public void Validate_WhenValid_IsValid()
    {
        var validator = new CreateOrderItemValidator();
        var result = validator.Validate(new CreateOrderItem("p-1", 2));

        result.IsValid.Should().BeTrue();
    }
}