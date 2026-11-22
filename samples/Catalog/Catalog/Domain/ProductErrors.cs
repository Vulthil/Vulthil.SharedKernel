using Vulthil.Results;

namespace Catalog.Domain;

public static class ProductErrors
{
    public static readonly Error InvalidSku =
        Error.Validation("Product.InvalidSku", "A SKU is three capital letters, a dash and four digits, for example ABC-1234.");

    public static readonly Error NameRequired =
        Error.Validation("Product.NameRequired", "A product needs a name.");

    public static readonly Error PriceMustBePositive =
        Error.Validation("Product.PriceMustBePositive", "A product needs a price above zero.");

    public static Error SkuTaken(string sku) =>
        Error.Conflict("Product.SkuTaken", $"A product with SKU {sku} already exists.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("Product.NotFound", $"Product {id} was not found.");
}
