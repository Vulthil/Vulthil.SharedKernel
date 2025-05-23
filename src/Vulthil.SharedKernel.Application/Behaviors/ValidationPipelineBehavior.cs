using FluentValidation;
using FluentValidation.Results;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application.Behaviors;

internal sealed class ValidationPipelineBehavior<TCommand, TResponse>(IEnumerable<IValidator<TCommand>> validators) :
    IPipelineHandler<TCommand, TResponse>
    where TCommand : IHaveResponse<TResponse>
{
    private readonly IEnumerable<IValidator<TCommand>> _validators = validators;

    public async Task<TResponse> HandleAsync(TCommand request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var validationFailures = await ValidateAsync(request);

        if (validationFailures.Length == 0)
        {
            return await next(cancellationToken);
        }

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod(nameof(Result.ValidationFailure));

            if (failureMethod is not null)
            {
                return (TResponse)failureMethod.Invoke(null, [CreateValidationError(validationFailures)])!;
            }

        }
        else if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(CreateValidationError(validationFailures));
        }

        throw new ValidationException(validationFailures);
    }

    private async Task<ValidationFailure[]> ValidateAsync(TCommand command)
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

    private static ValidationError CreateValidationError(ValidationFailure[] validationFailures) =>
        new(validationFailures.Select(f => Error.Problem(f.ErrorCode, f.ErrorMessage)).ToArray());
}
