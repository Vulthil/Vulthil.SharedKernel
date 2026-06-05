using System.Text.Json;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Abstractions.Tests.Consumers;

public sealed class MessageContextSnapshotTests : BaseUnitTestCase
{
    private static JsonSerializerOptions Options => new() { WriteIndented = false };

    [Fact]
    public void NewSnapshotShouldDefaultRoutingKeyToEmptyString()
    {
        // Arrange & Act
        var snapshot = new MessageContextSnapshot();

        // Assert
        snapshot.RoutingKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void NewSnapshotShouldDefaultRetryCountToZero()
    {
        // Arrange & Act
        var snapshot = new MessageContextSnapshot();

        // Assert
        snapshot.RetryCount.ShouldBe(0);
    }

    [Fact]
    public void NewSnapshotShouldDefaultOptionalMembersToNull()
    {
        // Arrange & Act
        var snapshot = new MessageContextSnapshot();

        // Assert
        snapshot.MessageId.ShouldBeNull();
        snapshot.RequestId.ShouldBeNull();
        snapshot.CorrelationId.ShouldBeNull();
        snapshot.ConversationId.ShouldBeNull();
        snapshot.InitiatorId.ShouldBeNull();
        snapshot.SourceAddress.ShouldBeNull();
        snapshot.DestinationAddress.ShouldBeNull();
        snapshot.ResponseAddress.ShouldBeNull();
        snapshot.FaultAddress.ShouldBeNull();
    }

    [Fact]
    public void SnapshotsWithSameValuesShouldBeEqual()
    {
        // Arrange
        var first = CreateFullSnapshot();
        var second = CreateFullSnapshot();

        // Act & Assert
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void SnapshotsWithDifferentValuesShouldNotBeEqual()
    {
        // Arrange
        var first = CreateFullSnapshot();
        var second = CreateFullSnapshot() with { RetryCount = 99 };

        // Act & Assert
        first.ShouldNotBe(second);
    }

    [Fact]
    public void WithExpressionShouldProduceModifiedCopyWithoutMutatingOriginal()
    {
        // Arrange
        var original = CreateFullSnapshot();

        // Act
        var modified = original with { RoutingKey = "changed", RetryCount = 5 };

        // Assert
        modified.RoutingKey.ShouldBe("changed");
        modified.RetryCount.ShouldBe(5);
        original.RoutingKey.ShouldBe("order.placed");
        original.RetryCount.ShouldBe(3);
    }

    [Fact]
    public void SnapshotShouldRoundtripThroughJson()
    {
        // Arrange
        var original = CreateFullSnapshot();

        // Act
        var json = JsonSerializer.SerializeToUtf8Bytes(original, Options);
        var roundtripped = JsonSerializer.Deserialize<MessageContextSnapshot>(json, Options);

        // Assert
        roundtripped.ShouldNotBeNull();
        roundtripped.ShouldBe(original);
    }

    private static MessageContextSnapshot CreateFullSnapshot() => new()
    {
        MessageId = "msg-1",
        RequestId = "req-1",
        CorrelationId = "corr-1",
        ConversationId = "conv-1",
        InitiatorId = "init-1",
        SourceAddress = new Uri("queue:producer"),
        DestinationAddress = new Uri("queue:fulfillment"),
        ResponseAddress = new Uri("queue:reply"),
        FaultAddress = new Uri("queue:faults"),
        RoutingKey = "order.placed",
        RetryCount = 3,
    };
}
