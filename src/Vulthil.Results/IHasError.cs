namespace Vulthil.Results;

/// <summary>
/// A type that carries a structured <see cref="Results.Error"/>. The exception handler of
/// <c>Vulthil.SharedKernel.Api</c> answers an exception that implements it with the problem response its error maps
/// to — the same response a failed <see cref="Result"/> with that error produces.
/// </summary>
public interface IHasError
{
    /// <summary>
    /// Gets the structured error.
    /// </summary>
    Error Error { get; }
}
