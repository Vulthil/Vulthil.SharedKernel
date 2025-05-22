using FluentValidation;
using FluentValidation.Results;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Vulthil.SharedKernel.Application.Behaviors;

internal abstract class ValidationPipelineBehavior(IEnumerable<IValidator> validators)
{
    private readonly IEnumerable<IValidator> _validators = validators;
    protected async Task<ValidationFailure[]> ValidateAsync<TCommand>(TCommand command)
    {
        if (!_validators.Any())
        {
            return [];
        }

        var context = new ValidationContext<TCommand>(command);

        var validationResults = await Task.WhenAll(_validators
            .Select(v => v.ValidateAsync(context)));

        var validationFailures = validationResults
            .Where(validationResult => !validationResult.IsValid)
            .SelectMany(validationResult => validationResult.Errors)
            .ToArray();

        return validationFailures;
    }

    protected static ValidationError CreateValidationError(ValidationFailure[] validationFailures) =>
        new(validationFailures.Select(f => Error.Problem(f.ErrorCode, f.ErrorMessage)).ToArray());
}

internal sealed class ValidationPipelineBehavior<TCommand>(
    IEnumerable<IValidator<TCommand>> validators,
    ICommandHandler<TCommand> innerHandler)
    : ValidationPipelineBehavior(validators), ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    private readonly ICommandHandler<TCommand> _innerHandler = innerHandler;

    public async Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var validationFailures = await ValidateAsync(command);

        if (validationFailures.Length == 0)
        {
            return await _innerHandler.HandleAsync(command, cancellationToken);
        }

        return Result.Failure(CreateValidationError(validationFailures));
    }
}
internal sealed class ValidationPipelineBehavior<TCommand, TResponse>(
    IEnumerable<IValidator<TCommand>> validators,
    ICommandHandler<TCommand, TResponse> innerHandler)
    : ValidationPipelineBehavior(validators), ICommandHandler<TCommand, TResponse>
    where TCommand : class, ICommand<TResponse>
    where TResponse : class
{
    private readonly ICommandHandler<TCommand, TResponse> _innerHandler = innerHandler;

    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var validationFailures = await ValidateAsync(command);

        if (validationFailures.Length == 0)
        {
            return await _innerHandler.HandleAsync(command, cancellationToken);
        }

        return Result.Failure<TResponse>(CreateValidationError(validationFailures));
    }
}
