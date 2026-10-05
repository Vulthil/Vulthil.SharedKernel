using System.Text.Json;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class MessageEnvelopeFactoryTests : BaseUnitTestCase
{
    [Fact]
    public void CreateSerializesThePayloadAsItsRuntimeTypeWhenTheCallerHoldsAnInterface()
    {
        // Arrange
        var shipmentId = Guid.NewGuid();

        // Act
        var envelope = MessageEnvelopeFactory.Create<IShipmentEvent>(
            new ShipmentDispatched(shipmentId, "Acme Freight"),
            new PublishContext(),
            messageId: "msg-1",
            correlationId: "corr-1",
            urn: new Uri("urn:message:ShipmentDispatched"),
            jsonOptions: JsonSerializerOptions.Default);

        // Assert
        var shipment = envelope.Message.Deserialize<ShipmentDispatched>(JsonSerializerOptions.Default);
        shipment.ShouldBe(new ShipmentDispatched(shipmentId, "Acme Freight"));
    }

    public interface IShipmentEvent
    {
        Guid ShipmentId { get; }
    }

    public sealed record ShipmentDispatched(Guid ShipmentId, string Carrier) : IShipmentEvent;
}
