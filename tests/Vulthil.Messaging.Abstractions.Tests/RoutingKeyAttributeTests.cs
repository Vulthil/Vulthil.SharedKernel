using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Abstractions.Tests;

public sealed class RoutingKeyAttributeTests : BaseUnitTestCase
{
    [Fact]
    public void RoutingKeyAttributeShouldStorePattern()
    {
        // Arrange & Act
        var attribute = new RoutingKeyAttribute("test.pattern");

        // Assert
        attribute.Pattern.ShouldBe("test.pattern");
    }

    [Fact]
    public void RoutingKeyAttributeShouldBeApplicableToClass()
    {
        // Arrange & Act
        var attribute = typeof(RoutingKeyAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), false).FirstOrDefault() as AttributeUsageAttribute;

        // Assert
        attribute.ShouldNotBeNull();
        attribute!.ValidOn.ShouldBe(AttributeTargets.Class);
    }

    [Fact]
    public void ClassCanHaveRoutingKeyAttribute()
    {
        // Arrange & Act
        var attributes = typeof(TestConsumerWithAttribute).GetCustomAttributes(typeof(RoutingKeyAttribute), false);

        // Assert
        attributes.Length.ShouldBe(1);
        ((RoutingKeyAttribute)attributes[0]).Pattern.ShouldBe("order.*");
    }

    [RoutingKey("order.*")]
    private class TestConsumerWithAttribute { }
}
