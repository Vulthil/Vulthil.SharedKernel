using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;

namespace WebApi.Domain.SideEffects;

public sealed record SideEffectId(Guid Value);

public sealed class SideEffect : AggregateRoot<SideEffectId>
{
    public Guid MainEntityId { get; private set; }
    public IStatus Status { get; private set; }

    private SideEffect(Guid mainEntityId, IStatus status) : base(new(Guid.CreateVersion7()))
    {
        MainEntityId = mainEntityId;
        Status = status;
    }

    public static SideEffect Create(Guid mainEntityId, DateTimeOffset startTime)
    {
        var inProgressStatus = StatusFactory.InProgress(startTime);

        return new SideEffect(mainEntityId, inProgressStatus);
    }

    public Result UpdateStatus(DateTimeOffset completedTime, int value)
    {
        if (Status is CompletedStatus)
        {
            return Result.Failure(SideEffectErrors.AlreadyCompleted);
        }

        Status = StatusFactory.Completed(completedTime, value);

        return Result.Success();
    }
}
