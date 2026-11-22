using Catalog.Domain.Events;
using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;

namespace Catalog.Domain;

public sealed class Product : AggregateRoot<ProductId>
{
    private Product(ProductId id, string sku, string name, decimal price, DateTimeOffset createdAtUtc)
        : base(id)
    {
        Sku = sku;
        Name = name;
        Price = price;
        CreatedAtUtc = createdAtUtc;
    }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public decimal Price { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<Product> Create(string sku, string name, decimal price, DateTimeOffset createdAtUtc) =>
        Result.Success(new Product(ProductId.New(), sku, name.Trim(), price, createdAtUtc))
            .Ensure(product => product.Name.Length > 0, ProductErrors.NameRequired)
            .Ensure(product => product.Price > 0, ProductErrors.PriceMustBePositive)
            .Tap(product => product.Raise(new ProductAddedDomainEvent(product.Id)));
}
