using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;

namespace WebApi.Domain.SideEffects;

public sealed record SideEffectId(Guid Value);

public sealed class SideEffect : AggregateRoot<SideEffectId>
{
    public Guid MainEntityId { get; private set; }
    public Status Status { get; private set; }

    private SideEffect(Guid mainEntityId, Status status) : base(new(Guid.CreateVersion7()))
    {
        MainEntityId = mainEntityId;
        Status = status;
    }

    public static SideEffect Create(Guid mainEntityId, DateTimeOffset startTime)
    {
        var inProgressStatus = Status.InProgress(startTime);

        return new SideEffect(mainEntityId, inProgressStatus);
    }

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
