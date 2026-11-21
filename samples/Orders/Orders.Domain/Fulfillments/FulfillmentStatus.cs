using System.Text.Json.Serialization;

namespace Orders.Domain.Fulfillments;

[JsonDerivedType(typeof(FailedStatus), "Failed")]
[JsonDerivedType(typeof(InProgressStatus), "InProgress")]
[JsonDerivedType(typeof(CompletedStatus), "Completed")]
public abstract record FulfillmentStatus
{
    public sealed record FailedStatus(DateTimeOffset FailedTime, string ErrorMessage) : FulfillmentStatus;
    public sealed record InProgressStatus(DateTimeOffset ProgressTime) : FulfillmentStatus;
    public sealed record CompletedStatus(DateTimeOffset CompletedTime, int ScanCount) : FulfillmentStatus;
}

public static class FulfillmentStatusFactories
{
    extension(FulfillmentStatus)
    {
        public static FulfillmentStatus Failed(DateTimeOffset failedTime, string errorMessage) =>
            new FulfillmentStatus.FailedStatus(failedTime, errorMessage);
        public static FulfillmentStatus InProgress(DateTimeOffset progressTime) =>
            new FulfillmentStatus.InProgressStatus(progressTime);
        public static FulfillmentStatus Completed(DateTimeOffset completedTime, int scanCount) =>
            new FulfillmentStatus.CompletedStatus(completedTime, scanCount);
    }
}
