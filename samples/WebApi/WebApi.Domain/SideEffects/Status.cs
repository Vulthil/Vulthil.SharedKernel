namespace WebApi.Domain.SideEffects;

public abstract record Status;
public sealed record FailedStatus(DateTimeOffset FailedTime, string ErrorMessage) : Status;
public sealed record InProgressStatus(DateTimeOffset ProgressTime) : Status;
public sealed record CompletedStatus(DateTimeOffset CompletedTime, int Value) : Status;

public static class StatusFactory
{
    public static Status Failed(DateTimeOffset failedTime, string errorMessage) =>
        new FailedStatus(failedTime, errorMessage);
    public static Status InProgress(DateTimeOffset progressTime) =>
        new InProgressStatus(progressTime);

    public static Status Completed(DateTimeOffset completedTime, int value) =>
        new CompletedStatus(completedTime, value);
}
