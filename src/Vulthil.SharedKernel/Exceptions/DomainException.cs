using Vulthil.Results;

namespace Vulthil.SharedKernel.Exceptions;

public abstract class DomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="error">The error containing the information about what happened.</param>
    protected DomainException(Error error)
        : base(error.Description)
        => Error = error;

    public Error Error { get; }
}
