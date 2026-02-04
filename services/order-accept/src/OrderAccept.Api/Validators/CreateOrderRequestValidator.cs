using FluentValidation;
using OrderAccept.Application.Contracts.Requests;

namespace OrderAccept.Api.Validators;

public sealed class CreateOrderItemValidator : AbstractValidator<CreateOrderItem>
{
    public CreateOrderItemValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("ProductId is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0.");
    }
}

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required.");

        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage("Items collection is required.")
            .Must(items => items is { Count: > 0 })
            .WithMessage("At least one order item is required.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateOrderItemValidator());
    }
}
