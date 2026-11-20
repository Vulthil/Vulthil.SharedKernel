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
            Headers = BrokerOutboxMetadata.CaptureHeaders(
                new Dictionary<string, object?>
                {
                    ["text"] = "value",
                    ["count"] = 42,
                    ["big"] = 5_000_000_000L,
                    ["ratio"] = 1.5,
                    ["flag"] = true,
                },
                JsonSerializerOptions.Default),
        };

        // Act
        var roundTripped = JsonSerializer.Deserialize<BrokerOutboxMetadata>(JsonSerializer.Serialize(metadata))!;

        // Assert
        var headers = roundTripped.RestoreHeaders().ShouldNotBeNull();
        headers["text"].ShouldBeOfType<string>().ShouldBe("value");
        headers["count"].ShouldBeOfType<int>().ShouldBe(42);
        headers["big"].ShouldBeOfType<long>().ShouldBe(5_000_000_000L);
        headers["ratio"].ShouldBeOfType<double>().ShouldBe(1.5);
        headers["flag"].ShouldBeOfType<bool>().ShouldBe(true);
    }

    [Fact]
    public void HeadersRoundTripRematerializesComplexValuesAsDetachedJsonElements()
    {
        // Arrange
        var published = new Dictionary<string, object?>
        {
            ["shape"] = new Dictionary<string, int> { ["a"] = 1 },
            ["tags"] = new[] { "vip", "eu" },
        };
        var metadata = new BrokerOutboxMetadata { Headers = BrokerOutboxMetadata.CaptureHeaders(published, JsonSerializerOptions.Default) };

        // Act
        var roundTripped = JsonSerializer.Deserialize<BrokerOutboxMetadata>(JsonSerializer.Serialize(metadata))!;

        // Assert
        var headers = roundTripped.RestoreHeaders().ShouldNotBeNull();
        headers["shape"].ShouldBeOfType<JsonElement>().GetProperty("a").GetInt32().ShouldBe(1);
        headers["tags"].ShouldBeOfType<JsonElement>().GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void HeaderNamesRoundTripVerbatimUnderADictionaryKeyPolicy()
    {
        // Arrange
        var options = new JsonSerializerOptions { DictionaryKeyPolicy = JsonNamingPolicy.CamelCase };
        var published = new Dictionary<string, object?> { ["ConversationId"] = "conv-1", ["Tenant"] = "acme" };
        var metadata = new BrokerOutboxMetadata { Headers = BrokerOutboxMetadata.CaptureHeaders(published, options) };

        // Act
        var roundTripped = JsonSerializer.Deserialize<BrokerOutboxMetadata>(JsonSerializer.Serialize(metadata, options), options)!;

        // Assert
        var headers = roundTripped.RestoreHeaders().ShouldNotBeNull();
        headers.ShouldContainKey("ConversationId");
        headers.ShouldContainKey("Tenant");
        headers.ShouldNotContainKey("conversationId");
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
            Headers = BrokerOutboxMetadata.CaptureHeaders(source.Headers, JsonSerializerOptions.Default),
        };
        var relayed = JsonSerializer.Deserialize<BrokerOutboxMetadata>(JsonSerializer.Serialize(metadata))!;
        var target = new PublishContext();

        // Act
        target.AddHeaders(relayed.RestoreHeaders()!);

        // Assert
        target.ResponseAddress.ShouldBe(new Uri("queue:order-replies"));
        target.ConversationId.ShouldBe("conv-1");
    }

    [Fact]
    public void ARelayedMessageSurfacesTheSameConsumedHeadersAsADirectPublish()
    {
        // Arrange — the header values a publish carries, spanning every wire shape.
        var published = new Dictionary<string, object?>
        {
            ["tenant"] = "acme",
            ["attempt"] = 3,
            ["big"] = 5_000_000_000L,
            ["ratio"] = 1.5,
            ["critical"] = true,
            ["id"] = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ["shape"] = new Dictionary<string, int> { ["a"] = 1 },
        };
        var metadata = new BrokerOutboxMetadata { Headers = BrokerOutboxMetadata.CaptureHeaders(published, JsonSerializerOptions.Default) };

        // Act — direct: publish → wire → consume; relayed: capture → outbox row → relay publish → wire → consume.
        var direct = ConsumeThroughEnvelope(published);
        var captured = JsonSerializer.Deserialize<BrokerOutboxMetadata>(JsonSerializer.Serialize(metadata))!;
        var relayed = ConsumeThroughEnvelope(captured.RestoreHeaders()!);

        // Assert — same keys, same CLR types, same values.
        relayed.Keys.ShouldBe(direct.Keys, ignoreOrder: true);
        foreach (var key in direct.Keys)
        {
            (relayed[key]?.GetType()).ShouldBe(direct[key]?.GetType(), $"header '{key}'");
        }

        JsonSerializer.Serialize(relayed).ShouldBe(JsonSerializer.Serialize(direct));
    }

    private static IReadOnlyDictionary<string, object?> ConsumeThroughEnvelope(IDictionary<string, object?> headers)
    {
        var envelope = new MessageEnvelope
        {
            MessageType = new Uri("urn:message:Test"),
            Message = JsonSerializer.SerializeToElement(new { }),
            Headers = new Dictionary<string, object?>(headers),
        };
        var wireEnvelope = JsonSerializer.Deserialize<MessageEnvelope>(JsonSerializer.Serialize(envelope))!;

        return MessageContext.CreateFromEnvelope(
            new object(),
            wireEnvelope,
            routingKey: string.Empty,
            redelivered: false,
            retryCount: 0,
            replyToFallback: null,
            publisher: null,
            sendEndpointProvider: null,
            CancellationToken.None).Headers;
    }
}
