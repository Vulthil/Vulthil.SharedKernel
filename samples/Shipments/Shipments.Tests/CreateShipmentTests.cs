using Shipments.Application.Shipments;
using Shipments.Messages;
using Shipments.Tests.Fixtures;
using Shouldly;
using Vulthil.Extensions.Testing;

namespace Shipments.Tests;

public sealed class CreateShipmentTests(HarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : HarnessTestCase(factory, testOutputHelper)
{
    private static readonly DateTimeOffset Estimate = new(2026, 12, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateShipment_creates_a_shipment_with_the_carrier_estimate()
    {
        // Arrange
        var orderReference = Guid.NewGuid();
        Harness.Respond<GetDeliveryEstimate, DeliveryEstimate>(_ => new DeliveryEstimate(Estimate));

        // Act
        await SendCreateShipmentAsync(new CreateShipment(orderReference, "Ada Lovelace"));

        // Assert
        var shipments = await GetShipmentsAsync(orderReference);
        var shipment = shipments.ShouldHaveSingleItem();
        shipment.Recipient.ShouldBe("Ada Lovelace");
        shipment.Carrier.ShouldBe("acme-express");
        shipment.EstimatedDeliveryUtc.ShouldBe(Estimate);
        Harness.Requested<GetDeliveryEstimate>().ShouldContain(request => request.Message.Recipient == "Ada Lovelace");
        Harness.Sent<CreateShipment>().ShouldContain(sent => sent.Message.OrderReference == orderReference && sent.DestinationAddress == new Uri($"queue:{QueueNames.ShipmentCommands}"));
    }

    [Fact]
    public async Task CreateShipment_still_creates_the_shipment_when_the_carrier_fails_to_answer()
    {
        // Arrange
        var orderReference = Guid.NewGuid();
        Harness.Respond<GetDeliveryEstimate, DeliveryEstimate>(_ => throw new InvalidOperationException("The carrier is down."));

        // Act
        await SendCreateShipmentAsync(new CreateShipment(orderReference, "Grace Hopper"));

        // Assert
        var shipment = (await GetShipmentsAsync(orderReference)).ShouldHaveSingleItem();
        shipment.EstimatedDeliveryUtc.ShouldBeNull();
    }

    [Fact]
    public async Task CreateShipment_redelivered_with_the_same_message_id_creates_one_shipment()
    {
        // Arrange
        var orderReference = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();
        Harness.Respond<GetDeliveryEstimate, DeliveryEstimate>(_ => new DeliveryEstimate(Estimate));

        // Act
        await SendCreateShipmentAsync(new CreateShipment(orderReference, "Ada"), messageId);
        await SendCreateShipmentAsync(new CreateShipment(orderReference, "Ada"), messageId);

        // Assert
        Harness.Sent<CreateShipment>().Count(sent => sent.Message.OrderReference == orderReference).ShouldBe(2);
        (await GetShipmentsAsync(orderReference)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CreateShipment_without_recipient_is_rejected_without_a_shipment()
    {
        // Arrange
        var orderReference = Guid.NewGuid();
        Harness.Respond<GetDeliveryEstimate, DeliveryEstimate>(_ => new DeliveryEstimate(Estimate));

        // Act
        await SendCreateShipmentAsync(new CreateShipment(orderReference, " "));

        // Assert
        (await GetShipmentsAsync(orderReference)).ShouldBeEmpty();
    }

    private async Task<IReadOnlyList<ShipmentDto>> GetShipmentsAsync(Guid orderReference)
    {
        var response = await Client.GetAsync($"shipments?orderReference={orderReference}", CancellationToken);
        return await response.GetResponseAsync<List<ShipmentDto>>(CancellationToken);
    }
}
