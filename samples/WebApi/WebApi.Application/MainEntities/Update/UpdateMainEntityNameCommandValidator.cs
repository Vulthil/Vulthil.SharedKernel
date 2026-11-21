using FluentValidation;
using Vulthil.SharedKernel.Application;
using WebApi.Domain.MainEntities;

namespace WebApi.Application.MainEntities.Update;

/// <summary>
/// <c>WithError</c> reuses the domain's error: the 400 response lists <c>MainEntity.NameTooLong</c> under
/// <c>errorCodes</c> instead of FluentValidation's validator name.
/// </summary>
public sealed class UpdateMainEntityNameCommandValidator : AbstractValidator<UpdateMainEntityNameCommand>
{
    public UpdateMainEntityNameCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().WithError(MainEntityErrors.NameRequired);
        RuleFor(command => command.Name).MaximumLength(MainEntity.MaxNameLength).WithError(MainEntityErrors.NameTooLong);
    }
}
