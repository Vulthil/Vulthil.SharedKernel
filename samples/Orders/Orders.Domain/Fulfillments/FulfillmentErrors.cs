using Vulthil.Results;

namespace Orders.Domain.Fulfillments;

public static class FulfillmentErrors
{
    public static readonly Error AlreadyCompleted =
        Error.Conflict("Fulfillment.AlreadyCompleted", "The fulfillment is already completed.");

    public static Error NotFound(Guid id) => Error.NotFound("Fulfillment.NotFound", $"Fulfillment {id} was not found.");
}
