namespace Catalog.Search.Searching;

public sealed record ProductSearchResult(Guid ProductId, string Sku, string Name, decimal Price);
