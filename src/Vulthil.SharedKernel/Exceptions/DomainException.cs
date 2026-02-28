using Vulthil.Results;

namespace Vulthil.SharedKernel.Exceptions;

/// <summary>
/// Base exception for domain rule violations, carrying a structured <see cref="Error"/>.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="error">The error containing the information about what happened.</param>
    protected DomainException(Error error)
        : base(error.Description)
        => Error = error;

    /// <summary>
    /// Gets the structured error describing the domain rule violation.
    /// </summary>
    public Error Error { get; }
}
