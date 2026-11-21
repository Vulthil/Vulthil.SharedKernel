using FluentValidation;
using Orders.Domain.Orders;
using Vulthil.SharedKernel.Application;

namespace Orders.Application.Orders.ChangeDescription;

/// <summary>
/// <c>WithError</c> reuses the domain's error: the 400 response lists <c>Order.DescriptionTooLong</c> under
/// <c>errorCodes</c> instead of FluentValidation's validator name.
/// </summary>
public sealed class ChangeOrderDescriptionCommandValidator : AbstractValidator<ChangeOrderDescriptionCommand>
{
    public ChangeOrderDescriptionCommandValidator()
    {
        RuleFor(command => command.Description).NotEmpty().WithError(OrderErrors.DescriptionRequired);
        RuleFor(command => command.Description).MaximumLength(Order.MaxDescriptionLength).WithError(OrderErrors.DescriptionTooLong);
    }
}
