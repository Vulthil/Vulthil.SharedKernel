using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;

namespace Orders.Domain.Fulfillments;

/// <summary>
/// Tracks an order from the moment it is placed until the Shipments service reports it delivered.
/// </summary>
public sealed class Fulfillment : AggregateRoot<FulfillmentId>
{
    private Fulfillment(Guid orderId, FulfillmentStatus status) : base(FulfillmentId.New())
    {
        OrderId = orderId;
        Status = status;
    }

    public Guid OrderId { get; private set; }

    public FulfillmentStatus Status { get; private set; }

    public static Fulfillment Start(Guid orderId, DateTimeOffset startedAtUtc) =>
        new(orderId, FulfillmentStatus.InProgress(startedAtUtc));

    public Result Complete(DateTimeOffset deliveredAtUtc, int scanCount)
    {
        if (Status is FulfillmentStatus.CompletedStatus)
        {
            return Result.Failure(FulfillmentErrors.AlreadyCompleted);
        }

        Status = FulfillmentStatus.Completed(deliveredAtUtc, scanCount);
        return Result.Success();
    }
}
