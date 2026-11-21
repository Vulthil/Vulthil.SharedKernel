using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Shipments.Application.Shipments;
using Shipments.Endpoints;
using Shipments.Messages;
using Shipments.Tests.Fixtures;
using Shouldly;
using Vulthil.Extensions.Testing;

namespace Shipments.Tests;

public sealed class ShipmentsEndpointTests(HarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : HarnessTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task Get_unknown_shipment_returns_a_not_found_problem()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        using var response = await Client.GetAsync($"shipments/{id}", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        ((string?)problem["detail"]).ShouldBe($"Shipment {id} was not found.");
    }

    [Fact]
    public async Task RescheduleDelivery_stores_the_new_estimate()
    {
        // Arrange
        var shipment = await CreateShipmentAsync();
        var estimate = DateTimeOffset.UtcNow.AddDays(5);

        // Act
        using var response = await Client.PutAsJsonAsync($"shipments/{shipment.Id}/estimated-delivery", new RescheduleDelivery.Request(estimate), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rescheduled = await (await Client.GetAsync($"shipments/{shipment.Id}", CancellationToken)).GetResponseAsync<ShipmentDto>(CancellationToken);
        rescheduled.EstimatedDeliveryUtc.ShouldBe(estimate);
    }

    [Fact]
    public async Task RescheduleDelivery_into_the_past_returns_the_domain_error_code_of_the_validator()
    {
        // Arrange
        var shipment = await CreateShipmentAsync();

        // Act
        using var response = await Client.PutAsJsonAsync($"shipments/{shipment.Id}/estimated-delivery", new RescheduleDelivery.Request(DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(CancellationToken))!;
        problem["errorCodes"]!["EstimatedDeliveryUtc"]!.AsArray().Select(code => (string?)code).ShouldBe(["Shipment.EstimateInThePast"]);
    }

    [Fact]
    public async Task RescheduleDelivery_of_a_delivered_shipment_returns_a_conflict_problem()
    {
        // Arrange
        var shipment = await CreateShipmentAsync();
        await Publisher.PublishAsync(TrackingUpdates.Delivered(shipment.Id, "Hamburg", DateTimeOffset.UtcNow), CancellationToken);

        // Act
        using var response = await Client.PutAsJsonAsync($"shipments/{shipment.Id}/estimated-delivery", new RescheduleDelivery.Request(DateTimeOffset.UtcNow.AddDays(1)), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private async Task<ShipmentDto> CreateShipmentAsync()
    {
        var orderReference = Guid.NewGuid();
        Harness.Respond<GetDeliveryEstimate, DeliveryEstimate>(_ => new DeliveryEstimate(DateTimeOffset.UtcNow.AddDays(2)));
        await SendCreateShipmentAsync(new CreateShipment(orderReference, "Ada"));

        var response = await Client.GetAsync($"shipments?orderReference={orderReference}", CancellationToken);
        return (await response.GetResponseAsync<List<ShipmentDto>>(CancellationToken)).ShouldHaveSingleItem();
    }
}
