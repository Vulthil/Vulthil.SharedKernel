using System.Text.Json.Serialization;

namespace WebApi.Domain.SideEffects;

[JsonDerivedType(typeof(FailedStatus), "Failed")]
[JsonDerivedType(typeof(InProgressStatus), "InProgress")]
[JsonDerivedType(typeof(CompletedStatus), "Completed")]
public abstract record Status
{
    public sealed record FailedStatus(DateTimeOffset FailedTime, string ErrorMessage) : Status;
    public sealed record InProgressStatus(DateTimeOffset ProgressTime) : Status;
    public sealed record CompletedStatus(DateTimeOffset CompletedTime, int Value) : Status;
}

public static class StatusFactories
{
    extension(Status)
    {
        public static Status Failed(DateTimeOffset failedTime, string errorMessage) =>
            new Status.FailedStatus(failedTime, errorMessage);
        public static Status InProgress(DateTimeOffset progressTime) =>
            new Status.InProgressStatus(progressTime);
        public static Status Completed(DateTimeOffset completedTime, int value) =>
            new Status.CompletedStatus(completedTime, value);
    }
}
