using FluentValidation;

namespace WebApi.Application.MainEntities.CreateMany;

public sealed class CreateMainEntitiesCommandValidator : AbstractValidator<CreateMainEntitiesCommand>
{
    public CreateMainEntitiesCommandValidator() => RuleFor(command => command.Names).NotEmpty();
}
