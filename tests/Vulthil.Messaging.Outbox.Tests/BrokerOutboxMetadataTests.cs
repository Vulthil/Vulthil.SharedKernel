using System.Text.Json;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Outbox.Tests;

public sealed class BrokerOutboxMetadataTests : BaseUnitTestCase
{
    [Fact]
    public void HeadersRoundTripPreservesPrimitiveClrTypesInsteadOfJsonElement()
    {
        // Arrange
        var metadata = new BrokerOutboxMetadata
        {
            Headers = new Dictionary<string, object?>
            {
                ["text"] = "value",
                ["count"] = 42,
                ["flag"] = true,
            },
        };

        // Act
        var roundTripped = JsonSerializer.Deserialize<BrokerOutboxMetadata>(JsonSerializer.Serialize(metadata))!;

        // Assert
        var headers = roundTripped.Headers.ShouldNotBeNull();
        headers["text"].ShouldBeOfType<string>().ShouldBe("value");
        headers["count"].ShouldBeOfType<long>().ShouldBe(42L);
        headers["flag"].ShouldBeOfType<bool>().ShouldBe(true);
    }

    [Fact]
    public void RelayedHeadersRestoreTypedPublishContextGetters()
    {
        // Arrange
        var source = new PublishContext();
        source.SetResponseAddress(new Uri("queue:order-replies"));
        source.SetConversationId("conv-1");
        var metadata = new BrokerOutboxMetadata
        {
            Headers = new Dictionary<string, object?>(source.Headers),
        };
        var relayed = JsonSerializer.Deserialize<BrokerOutboxMetadata>(JsonSerializer.Serialize(metadata))!;
        var target = new PublishContext();

        // Act
        target.AddHeaders(relayed.Headers!);

        // Assert
        target.ResponseAddress.ShouldBe(new Uri("queue:order-replies"));
        target.ConversationId.ShouldBe("conv-1");
    }
}
