using FluentValidation;

namespace Orders.Application.Orders.Ship;

public sealed class ShipOrderCommandValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderCommandValidator() => RuleFor(command => command.Recipient).NotEmpty().MaximumLength(200);
}
