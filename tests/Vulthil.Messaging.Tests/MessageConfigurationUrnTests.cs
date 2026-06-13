using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class MessageConfigurationUrnTests : BaseUnitTestCase
{
    [Fact]
    public void DefaultUrnUsesColonSeparatedNamespace()
    {
        // Act
        var config = new MessageConfiguration<NamespacedMessage>();

        // Assert
        config.Urn.AbsoluteUri.ShouldBe($"urn:message:{typeof(NamespacedMessage).Namespace}:{nameof(NamespacedMessage)}");
    }

    [Fact]
    public void DefaultUrnHandlesGlobalNamespace()
    {
        // Arrange
        var config = new MessageConfiguration("Globally");

        // Assert
        config.Urn.AbsoluteUri.ShouldBe("urn:message:Globally");
    }

    [Fact]
    public void ExplicitUrnOverridesDefault()
    {
        // Arrange
        var config = new MessageConfiguration<NamespacedMessage>
        {
            Urn = new Uri("urn:message:Acme.Orders:OrderPlaced")
        };

        // Assert
        config.Urn.AbsoluteUri.ShouldBe("urn:message:Acme.Orders:OrderPlaced");
    }
}

internal sealed record NamespacedMessage(string Content);
