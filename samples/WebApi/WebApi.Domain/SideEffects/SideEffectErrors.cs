using Vulthil.Results;

namespace WebApi.Domain.SideEffects;

/// <summary>
/// Represents the SideEffectErrors.
/// </summary>
public static class SideEffectErrors
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    public static Error AlreadyCompleted => Error.Conflict("SideEffect.AlreadyCompleted", "The side effect has already been completed and cannot be updated again.");
}
