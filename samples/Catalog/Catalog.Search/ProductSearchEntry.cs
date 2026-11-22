namespace Catalog.Search;

/// <summary>
/// The search read model of a product. It is not an aggregate: it raises no events and is rebuilt from
/// <see cref="Contracts.ProductPublished"/>, so writing it twice with the same values is harmless.
/// </summary>
public sealed class ProductSearchEntry
{
    private ProductSearchEntry(string id, Guid productId, string sku, string name, decimal price, DateTimeOffset indexedAtUtc)
    {
        Id = id;
        ProductId = productId;
        Sku = sku;
        Name = name;
        NormalizedName = Normalize(name);
        Price = price;
        IndexedAtUtc = indexedAtUtc;
    }

    public string Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public decimal Price { get; private set; }

    public DateTimeOffset IndexedAtUtc { get; private set; }

    public static string IdFor(Guid productId) => productId.ToString("N");

    public static string Normalize(string text) => text.Trim().ToUpperInvariant();

    public static ProductSearchEntry Create(Guid productId, string sku, string name, decimal price, DateTimeOffset indexedAtUtc) =>
        new(IdFor(productId), productId, sku, name, price, indexedAtUtc);

    public void Update(string sku, string name, decimal price, DateTimeOffset indexedAtUtc)
    {
        Sku = sku;
        Name = name;
        NormalizedName = Normalize(name);
        Price = price;
        IndexedAtUtc = indexedAtUtc;
    }
}
