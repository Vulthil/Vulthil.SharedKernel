namespace WebApi.Domain.SideEffects;

public interface IStatus;
public sealed record FailedStatus(DateTimeOffset FailedTime, string ErrorMessage) : IStatus;
public sealed record InProgressStatus(DateTimeOffset ProgressTime) : IStatus;
public sealed record CompletedStatus(DateTimeOffset CompletedTime, int Value) : IStatus;

public static class StatusFactory
{
    public static IStatus Failed(DateTimeOffset failedTime, string errorMessage) =>
        new FailedStatus(failedTime, errorMessage);
    public static IStatus InProgress(DateTimeOffset progressTime) =>
        new InProgressStatus(progressTime);

    public static IStatus Completed(DateTimeOffset completedTime, int value) =>
        new CompletedStatus(completedTime, value);
}
