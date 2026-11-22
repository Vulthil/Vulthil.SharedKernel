using Catalog.Domain;
using Catalog.Domain.Events;
using Shouldly;
using Vulthil.xUnit;

namespace Catalog.Tests;

public sealed class ProductTests : BaseUnitTestCase
{
    [Fact]
    public void Create_raises_ProductAddedDomainEvent()
    {
        // Act
        var result = Product.Create("ABC-1234", " Espresso cup ", 12.5m, DateTimeOffset.UnixEpoch);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Espresso cup");
        result.Value.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ProductAddedDomainEvent>().ProductId.ShouldBe(result.Value.Id);
    }

    [Fact]
    public void Create_with_a_price_of_zero_fails()
    {
        // Act
        var result = Product.Create("ABC-1234", "Espresso cup", 0m, DateTimeOffset.UnixEpoch);

        // Assert
        result.Error.ShouldBe(ProductErrors.PriceMustBePositive);
    }
}
