using Orders.Domain.Orders.Events;
using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;

namespace Orders.Domain.Orders;

public sealed class Order : AggregateRoot<OrderId>
{
    public const int MaxDescriptionLength = 100;

    private Order(string description) : base(OrderId.New()) => Description = description;

    public string Description { get; private set; }

    public DateTimeOffset? ShippedAtUtc { get; private set; }

    public static Result<Order> Place(string description) =>
        ValidateDescription(description)
            .Map(validDescription => new Order(validDescription))
            .Tap(order => order.Raise(new OrderPlacedDomainEvent(order.Id)));

    public Result ChangeDescription(string description) =>
        ValidateDescription(description)
            .Ensure(validDescription => validDescription != Description, OrderErrors.DescriptionUnchanged)
            .Bind(validDescription =>
            {
                Description = validDescription;
                Raise(new OrderDescriptionChangedDomainEvent(Id, Description));
                return Result.Success();
            });

    /// <summary>
    /// Records that the order was handed to the Shipments service. Shipping an order twice would send a second
    /// shipment, so a second call breaks an invariant and throws <see cref="OrderAlreadyShippedException"/>; the API
    /// answers it with the problem response of its error.
    /// </summary>
    public void MarkShipped(DateTimeOffset shippedAtUtc)
    {
        if (ShippedAtUtc is not null)
        {
            throw new OrderAlreadyShippedException(Id.Value);
        }

        ShippedAtUtc = shippedAtUtc;
    }

    private static Result<string> ValidateDescription(string description) =>
        Result.Success(description?.Trim() ?? string.Empty)
            .Ensure(trimmed => trimmed.Length > 0, OrderErrors.DescriptionRequired)
            .Ensure(trimmed => trimmed.Length <= MaxDescriptionLength, OrderErrors.DescriptionTooLong);
}
