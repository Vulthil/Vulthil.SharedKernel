using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;

namespace WebApi.Domain.SideEffects;

/// <summary>
/// Represents the SideEffectId.
/// </summary>
public sealed record SideEffectId(Guid Value);

/// <summary>
/// Represents the SideEffect.
/// </summary>
public sealed class SideEffect : AggregateRoot<SideEffectId>
{
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public Guid MainEntityId { get; private set; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public Status Status { get; private set; }

    private SideEffect(Guid mainEntityId, Status status) : base(new(Guid.CreateVersion7()))
    {
        MainEntityId = mainEntityId;
        Status = status;
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    public static SideEffect Create(Guid mainEntityId, DateTimeOffset startTime)
    {
        var inProgressStatus = Status.InProgress(startTime);

        return new SideEffect(mainEntityId, inProgressStatus);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    public Result Complete(DateTimeOffset completedTime, int value)
    {
        if (Status is Status.CompletedStatus)
        {
            return Result.Failure(SideEffectErrors.AlreadyCompleted);
        }

        Status = Status.Completed(completedTime, value);

        return Result.Success();
    }
}
