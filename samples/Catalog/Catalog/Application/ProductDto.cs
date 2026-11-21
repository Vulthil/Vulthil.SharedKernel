using Catalog.Domain;

namespace Catalog.Application;

public sealed record ProductDto(Guid Id, string Sku, string Name, decimal Price, DateTimeOffset CreatedAtUtc)
{
    public static ProductDto FromModel(Product product) =>
        new(product.Id.Value, product.Sku, product.Name, product.Price, product.CreatedAtUtc);
}
