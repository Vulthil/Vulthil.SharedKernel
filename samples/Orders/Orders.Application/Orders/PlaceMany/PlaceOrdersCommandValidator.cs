using FluentValidation;

namespace Orders.Application.Orders.PlaceMany;

public sealed class PlaceOrdersCommandValidator : AbstractValidator<PlaceOrdersCommand>
{
    public PlaceOrdersCommandValidator() => RuleFor(command => command.Descriptions).NotEmpty();
}
