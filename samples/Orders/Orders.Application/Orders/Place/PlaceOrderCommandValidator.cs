using FluentValidation;

namespace WebApi.Application.MainEntities.Create;

public sealed class CreateMainEntityCommandValidator : AbstractValidator<CreateMainEntityCommand>
{
    public CreateMainEntityCommandValidator() => RuleFor(command => command.Name).NotEmpty();
}
