using FluentValidation.Results;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Behaviors;

/// <summary>
/// Turns FluentValidation failures into the <see cref="ValidationError"/> the validation pipeline reports, whether as a
/// failed result or through a <see cref="CommandValidationException"/>.
/// </summary>
internal static class ValidationFailureConversion
{
    /// <summary>
    /// Creates one <see cref="ValidationError"/> with one inner error per failure: the failure's error code as the code
    /// and the failure's message as the description.
    /// </summary>
    /// <param name="failures">The validation failures. Must contain at least one failure.</param>
    /// <returns>The validation error.</returns>
    public static ValidationError ToValidationError(IEnumerable<ValidationFailure> failures)
        => new(failures.Select(failure => Error.Validation(failure.ErrorCode, failure.ErrorMessage)));
}
