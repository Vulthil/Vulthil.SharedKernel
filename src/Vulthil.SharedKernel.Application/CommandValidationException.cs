using FluentValidation;
using FluentValidation.Results;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Behaviors;

namespace Vulthil.SharedKernel.Application;

/// <summary>
/// The <see cref="ValidationException"/> the validation pipeline behavior throws when a command that does not return a
/// <see cref="Result"/> fails validation, because such a command has no in-band way to report the failure. It carries
/// the failures as a <see cref="ValidationError"/> through <see cref="IHasError"/>, so the exception handler of
/// <c>Vulthil.SharedKernel.Api</c> answers it with the same 400 validation problem a failed result produces. Code that
/// catches <see cref="ValidationException"/> keeps working.
/// </summary>
public sealed class CommandValidationException : ValidationException, IHasError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandValidationException"/> class.
    /// </summary>
    /// <param name="failures">The validation failures. Must contain at least one failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failures"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="failures"/> contains no failures.</exception>
    public CommandValidationException(IEnumerable<ValidationFailure> failures)
        : base(failures ?? throw new ArgumentNullException(nameof(failures)))
        => Error = ValidationFailureConversion.ToValidationError(Errors);

    /// <summary>
    /// Gets the failures as a <see cref="ValidationError"/>: one inner error per failure, with the failure's error code
    /// as its code and the failure's message as its description — the same error a failed result of the command
    /// would carry.
    /// </summary>
    public ValidationError Error { get; }

    Error IHasError.Error => Error;
}
