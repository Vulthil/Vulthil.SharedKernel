using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application.Behaviors;

/// <summary>
/// Pipeline behavior that runs FluentValidation validators before the command handler. On a validation failure it
/// short-circuits the pipeline: a command returning <see cref="Result"/> or <see cref="Result{T}"/> receives a failed
/// result carrying a <see cref="ValidationError"/>, whereas a command with any other response type throws a
/// <see cref="CommandValidationException"/> carrying the same error — there is no in-band way to represent failure for
/// a non-result response.
/// </summary>
internal sealed class ValidationPipelineBehavior<TCommand, TResponse>(IEnumerable<IValidator<TCommand>> validators) :
    IPipelineHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private static readonly MethodInfo? ValidationFailureMethod = CreateValidationFailureMethod();

    private readonly IEnumerable<IValidator<TCommand>> _validators = validators;

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(TCommand request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var validationFailures = await ValidateAsync(request, cancellationToken).ConfigureAwait(false);

        if (validationFailures.Length == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (ValidationFailureMethod is not null)
        {
            return (TResponse)ValidationFailureMethod.Invoke(null, [ValidationFailureConversion.ToValidationError(validationFailures)])!;
        }

        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(ValidationFailureConversion.ToValidationError(validationFailures));
        }

        throw new CommandValidationException(validationFailures);
    }

    private static MethodInfo? CreateValidationFailureMethod()
    {
        if (!typeof(TResponse).IsGenericType || typeof(TResponse).GetGenericTypeDefinition() != typeof(Result<>))
        {
            return null;
        }

        var resultType = typeof(TResponse).GetGenericArguments()[0];
        var openValidationFailureMethod = typeof(Result)
            .GetMethods()
            .Single(m => m.Name == nameof(Result.ValidationFailure) && m.IsGenericMethodDefinition);

        return openValidationFailureMethod.MakeGenericMethod(resultType);
    }

    /// <summary>
    /// Runs the validators one after another, each on a validation context of its own, and returns the failures of
    /// all of them. A FluentValidation result shares its context's failure list, so validators that shared one context
    /// would each report every failure. Validators also often use scoped services, such as a <c>DbContext</c>, that
    /// allow only one operation at a time.
    /// </summary>
    private async Task<ValidationFailure[]> ValidateAsync(TCommand command, CancellationToken cancellationToken)
    {
        List<ValidationFailure> failures = [];
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<TCommand>(command), cancellationToken).ConfigureAwait(false);
            failures.AddRange(result.Errors);
        }

        return [.. failures];
    }
}
