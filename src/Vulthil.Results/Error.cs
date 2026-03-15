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
    /// Gets the individual validation errors collected during validation.
    /// Each element represents a single field or rule violation.
    /// </summary>
    public Error[] Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> record.
    /// </summary>
    /// <param name="errors">The individual validation errors.</param>
    public ValidationError(Error[] errors)
        : base("Validation.General",
        "One or more validation errors occurred",
        ErrorType.Validation) => Errors = errors;

    /// <summary>
    /// Creates a <see cref="ValidationError"/> from a collection of failed results.
    /// </summary>
    /// <param name="results">The results to extract errors from.</param>
    /// <returns>A <see cref="ValidationError"/> containing only the errors from failed results.</returns>
    public static ValidationError FromResults(IEnumerable<Result> results) =>
        new(results.Where(r => r.IsFailure).Select(r => r.Error).ToArray());
}

/// <summary>
/// Specifies values for ErrorType.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Specifies the Failure value.
    /// </summary>
    Failure = 0,
    /// <summary>
    /// Specifies the Validation value.
    /// </summary>
    Validation = 1,
    /// <summary>
    /// Specifies the Problem value.
    /// </summary>
    Problem = 2,
    /// <summary>
    /// Specifies the NotFound value.
    /// </summary>
    NotFound = 3,
    /// <summary>
    /// Specifies the Conflict value.
    /// </summary>
    Conflict = 4,
}
