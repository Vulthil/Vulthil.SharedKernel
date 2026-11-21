using FluentValidation;

namespace Orders.Application.Orders.Place;

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator() => RuleFor(command => command.Description).NotEmpty();
}
