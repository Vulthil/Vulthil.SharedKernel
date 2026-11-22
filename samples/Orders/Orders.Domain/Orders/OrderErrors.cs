using Vulthil.Results;

namespace Orders.Domain.Orders;

public static class OrderErrors
{
    public static readonly Error DescriptionRequired =
        Error.Validation("Order.DescriptionRequired", "An order needs a description.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("Order.DescriptionTooLong", $"A description can have at most {Order.MaxDescriptionLength} characters.");

    public static readonly Error DescriptionUnchanged =
        Error.Conflict("Order.DescriptionUnchanged", "The order already has that description.");

    public static Error NotFound(Guid id) => Error.NotFound("Order.NotFound", $"Order {id} was not found.");

    public static Error AlreadyShipped(Guid id) =>
        Error.Conflict("Order.AlreadyShipped", $"Order {id} has already been shipped.");
}
