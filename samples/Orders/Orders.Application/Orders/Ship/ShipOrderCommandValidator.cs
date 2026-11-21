using FluentValidation;

namespace WebApi.Application.Shipping;

public sealed class ShipMainEntityCommandValidator : AbstractValidator<ShipMainEntityCommand>
{
    public ShipMainEntityCommandValidator() => RuleFor(command => command.Recipient).NotEmpty().MaximumLength(200);
}
