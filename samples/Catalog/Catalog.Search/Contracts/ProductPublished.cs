namespace Catalog.Search.Contracts;

/// <summary>
/// Published by the products part of the Catalog service when a product is added; the search module consumes it to
/// index the product. The publisher sets a message id derived from the product, so a relay that delivers it twice is
/// recognised by the inbox.
/// </summary>
public sealed record ProductPublished(Guid ProductId, string Sku, string Name, decimal Price)
{
    public static string MessageIdFor(Guid productId) => $"product-published-{productId:N}";
}
