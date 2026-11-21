using Shipments.Application.Tracking;
using Shipments.Messages;
using Shouldly;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Shipments.Tests;

public sealed class TrackingUpdateKeySelectorTests : BaseUnitTestCase<TrackingUpdateKeySelector>
{
    [Fact]
    public void GetKey_is_the_same_for_a_resent_scan_with_a_new_message_id()
    {
        // Arrange
        var shipmentId = Guid.NewGuid();
        var first = ContextFor(new ParcelPickedUp(shipmentId, "Copenhagen", DateTimeOffset.UnixEpoch), messageId: "first");
        var resent = ContextFor(new ParcelPickedUp(shipmentId, "Copenhagen", DateTimeOffset.UnixEpoch.AddMinutes(5)), messageId: "second");

        // Act
        var firstKey = Target.GetKey(first);
        var resentKey = Target.GetKey(resent);

        // Assert
        resentKey.ShouldBe(firstKey);
    }

    [Fact]
    public void GetKey_differs_per_kind_of_scan()
    {
        // Arrange
        var shipmentId = Guid.NewGuid();

        // Act
        var pickedUp = Target.GetKey(ContextFor(new ParcelPickedUp(shipmentId, "Copenhagen", DateTimeOffset.UnixEpoch), "1"));
        var delivered = Target.GetKey(ContextFor(new ParcelDelivered(shipmentId, "Hamburg", DateTimeOffset.UnixEpoch), "2"));

        // Assert
        pickedUp.ShouldNotBe(delivered);
    }

    private static IMessageContext<TrackingUpdate> ContextFor(TrackingUpdate update, string messageId)
    {
        var context = new Mock<IMessageContext<TrackingUpdate>>();
        context.SetupGet(c => c.Message).Returns(update);
        context.SetupGet(c => c.MessageId).Returns(messageId);
        return context.Object;
    }
}
