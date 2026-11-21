namespace Storefront.Api;

public sealed record ProductView(Guid ProductId, string Sku, string Name, decimal Price);
