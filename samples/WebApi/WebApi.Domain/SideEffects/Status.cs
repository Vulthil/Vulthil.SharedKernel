using System.Text.Json.Serialization;

namespace WebApi.Domain.SideEffects;

/// <summary>
/// Represents the Status.
/// </summary>
[JsonDerivedType(typeof(FailedStatus), "Failed")]
[JsonDerivedType(typeof(InProgressStatus), "InProgress")]
[JsonDerivedType(typeof(CompletedStatus), "Completed")]
public abstract record Status
{
    /// <summary>
    /// Represents the FailedStatus.
    /// </summary>
    public sealed record FailedStatus(DateTimeOffset FailedTime, string ErrorMessage) : Status;
    /// <summary>
    /// Represents the InProgressStatus.
    /// </summary>
    public sealed record InProgressStatus(DateTimeOffset ProgressTime) : Status;
    /// <summary>
    /// Represents the CompletedStatus.
    /// </summary>
    public sealed record CompletedStatus(DateTimeOffset CompletedTime, int Value) : Status;
}

/// <summary>
/// Represents the StatusFactories.
/// </summary>
public static class StatusFactories
{
    extension(Status)
    {
        /// <summary>
        /// Executes this member.
        /// </summary>
        public static Status Failed(DateTimeOffset failedTime, string errorMessage) =>
            new Status.FailedStatus(failedTime, errorMessage);
        /// <summary>
        /// Executes this member.
        /// </summary>
        public static Status InProgress(DateTimeOffset progressTime) =>
            new Status.InProgressStatus(progressTime);
        /// <summary>
        /// Executes this member.
        /// </summary>
        public static Status Completed(DateTimeOffset completedTime, int value) =>
            new Status.CompletedStatus(completedTime, value);
    }
}
