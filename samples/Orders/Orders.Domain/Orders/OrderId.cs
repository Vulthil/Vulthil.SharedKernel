namespace Orders.Domain.Orders;

public sealed record OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.CreateVersion7());
}
