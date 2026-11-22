using System.Text.RegularExpressions;
using Catalog.Domain;
using FluentValidation;
using Vulthil.SharedKernel.Application;

namespace Catalog.Application;

public sealed partial class AddProductCommandValidator : AbstractValidator<AddProductCommand>
{
    public AddProductCommandValidator()
    {
        RuleFor(command => command.Sku).Matches(SkuPattern()).WithError(ProductErrors.InvalidSku);
        RuleFor(command => command.Name).NotEmpty().WithError(ProductErrors.NameRequired);
        RuleFor(command => command.Price).GreaterThan(0).WithError(ProductErrors.PriceMustBePositive);
    }

    [GeneratedRegex("^[A-Z]{3}-[0-9]{4}$")]
    private static partial Regex SkuPattern();
}
