namespace Vulthil.Results;

/// <summary>
/// Represents a structured error with a code, description, and classification.
/// </summary>
public record Error
{
    /// <summary>
    /// Represents the absence of an error.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    /// <summary>
    /// Represents an error caused by a <see langword="null"/> value.
    /// </summary>
    public static readonly Error NullValue = new("General.NullValue", "Null value was provided", ErrorType.Failure);

    /// <summary>
    /// Gets the machine-readable error code used to identify the error category (e.g., "User.NotFound").
    /// Appears in validation problem details and can be used for programmatic error handling.
    /// </summary>
    public string Code { get; }
    /// <summary>
    /// Gets the human-readable error description suitable for logging or displaying to users.
    /// </summary>
    public string Description { get; }
    /// <summary>
    /// Gets the classification of the error, which determines how the error maps to HTTP status codes
    /// and problem-detail responses in the API layer.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Error"/> record.
    /// </summary>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="description">The human-readable error description.</param>
    /// <param name="type">The error classification.</param>
    protected internal Error(string code, string description, ErrorType type)
    {
        Code = code;
        Description = description;
        Type = type;
    }

    /// <summary>
    /// Creates a general failure error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A new <see cref="Error"/> with <see cref="ErrorType.Failure"/> classification.</returns>
    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);
    /// <summary>
    /// Creates a not-found error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A new <see cref="Error"/> with <see cref="ErrorType.NotFound"/> classification.</returns>
    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
    /// <summary>
    /// Creates a problem error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A new <see cref="Error"/> with <see cref="ErrorType.Problem"/> classification.</returns>
    public static Error Problem(string code, string description) => new(code, description, ErrorType.Problem);
    /// <summary>
    /// Creates a validation error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A new <see cref="Error"/> with <see cref="ErrorType.Validation"/> classification.</returns>
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="description">The error description.</param>
    /// <returns>A new <see cref="Error"/> with <see cref="ErrorType.Conflict"/> classification.</returns>
    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
}


/// <summary>
/// Represents a validation error containing one or more inner errors.
/// </summary>
public sealed record ValidationError : Error
{
    /// <summary>
    /// Gets the individual validation errors collected during validation. Always contains at least one error;
    /// each element represents a single field or rule violation.
    /// </summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> record.
    /// </summary>
    /// <param name="errors">The individual validation errors. Must contain at least one error.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> contains no errors.</exception>
    public ValidationError(IEnumerable<Error> errors)
        : base("Validation.General",
        "One or more validation errors occurred",
        ErrorType.Validation)
    {
        ArgumentNullException.ThrowIfNull(errors);

        Error[] copy = [.. errors];
        if (copy.Length == 0)
        {
            throw new ArgumentException("A validation error must contain at least one error.", nameof(errors));
        }

        Errors = copy;
    }

    /// <summary>
    /// Creates a <see cref="ValidationError"/> from a collection of failed results.
    /// </summary>
    /// <param name="results">The results to extract errors from. At least one must be a failure.</param>
    /// <returns>A <see cref="ValidationError"/> containing the errors from the failed results.</returns>
    /// <exception cref="ArgumentException">None of the <paramref name="results"/> is a failure.</exception>
    public static ValidationError FromResults(IEnumerable<Result> results) =>
        new(results.Where(r => r.IsFailure).Select(r => r.Error));

    /// <summary>
    /// Determines whether the specified validation error contains an equal, equally-ordered sequence of inner errors.
    /// </summary>
    /// <param name="other">The validation error to compare with the current one.</param>
    /// <returns><see langword="true"/> if both contain an equal sequence of errors; otherwise <see langword="false"/>.</returns>
    public bool Equals(ValidationError? other) =>
        other is not null && base.Equals(other) && Errors.SequenceEqual(other.Errors);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var error in Errors)
        {
            hash.Add(error);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Classifies an <see cref="Error"/> so that presentation layers (e.g. <c>Vulthil.SharedKernel.Api</c>) can map it
/// to the appropriate HTTP status code without inspecting its code or description.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// An unclassified or unexpected failure with no more specific meaning. Maps to HTTP 500 Internal Server Error.
    /// This is the default classification produced by <see cref="Error.Failure(string, string)"/>.
    /// </summary>
    Failure = 0,
    /// <summary>
    /// One or more input values failed validation (e.g. FluentValidation rule failures). Carried by
    /// <see cref="ValidationError"/>, whose <see cref="ValidationError.Errors"/> lists every individual violation.
    /// Maps to HTTP 400 Bad Request as a validation-problem response with per-field details.
    /// </summary>
    Validation = 1,
    /// <summary>
    /// A client-addressable business-rule violation the caller can act on (distinct from a plain validation
    /// failure). Maps to HTTP 400 Bad Request with the error's full detail in the problem response.
    /// </summary>
    Problem = 2,
    /// <summary>
    /// The requested resource does not exist. Maps to HTTP 404 Not Found.
    /// </summary>
    NotFound = 3,
    /// <summary>
    /// The request conflicts with the current state of the resource (e.g. a uniqueness violation). Maps to
    /// HTTP 409 Conflict.
    /// </summary>
    Conflict = 4,
}
