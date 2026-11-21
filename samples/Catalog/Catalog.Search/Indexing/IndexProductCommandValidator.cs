using FluentValidation;
using Vulthil.SharedKernel.Application;

namespace Catalog.Search.Indexing;

public sealed class IndexProductCommandValidator : AbstractValidator<IndexProductCommand>
{
    public IndexProductCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().WithError(SearchErrors.NameRequired);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0).WithError(SearchErrors.PriceNegative);
    }
}
