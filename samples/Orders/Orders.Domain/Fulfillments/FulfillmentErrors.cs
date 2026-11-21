using Vulthil.Results;

namespace WebApi.Domain.SideEffects;

public static class SideEffectErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("SideEffect.NotFound", $"Side effect {id} was not found.");

    public static Error AlreadyCompleted => Error.Conflict("SideEffect.AlreadyCompleted", "The side effect has already been completed and cannot be updated again.");
}
