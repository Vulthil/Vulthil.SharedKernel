namespace Catalog.Domain;

public sealed record ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.CreateVersion7());
}
