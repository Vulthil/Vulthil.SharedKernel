using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.xUnit;
using Orders.Application.Orders.Place;
using Orders.Application.Shipping;
using Orders.Application.Fulfillments;
using Orders.Application.Fulfillments.GetInProgress;
using Orders.Domain.Fulfillments;
using Orders.Endpoints;
using Orders.Tests.Fixtures;

namespace Orders.Tests;

public sealed class ShippingTests(HarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<HarnessWebApplicationFactory>
{
    private ISender Sender => ScopedServices.GetRequiredService<ISender>();

    private ITestHarness Harness => ScopedServices.GetRequiredService<ITestHarness>();

    [Fact]
    public async Task Ship_sends_CreateShipment_to_the_shipments_queue_once_the_command_commits()
    {
        // Arrange
        var orderId = await PlaceOrderAsync();

        // Act
        using var response = await Client.PostAsJsonAsync($"api/orders/{orderId}/shipments", new ShipOrder.Request("Ada Lovelace"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var relayed = await Polling.WaitAsync(
            new PollingOptions(TimeSpan.FromSeconds(20)) { TimerTick = TimeSpan.FromMilliseconds(250) },
            _ => Task.FromResult(Harness.Sent<CreateShipment>().Any(sent => sent.Message.OrderReference == orderId)),
            CancellationToken);
        relayed.IsSuccess.ShouldBeTrue();

        var sent = Harness.Sent<CreateShipment>().Single(captured => captured.Message.OrderReference == orderId);
        sent.DestinationAddress.ShouldBe(new Uri("queue:shipment-commands"));
        sent.Envelope.MessageType.ShouldBe(new Uri(CreateShipment.Urn));
        sent.Envelope.Headers!["source-service"]!.ToString().ShouldBe("Orders");
    }

    [Fact]
    public async Task Ship_twice_answers_the_domain_exception_with_a_conflict_problem()
    {
        // Arrange
        var orderId = await PlaceOrderAsync();
        using var first = await Client.PostAsJsonAsync($"api/orders/{orderId}/shipments", new ShipOrder.Request("Ada"), CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Act
        using var second = await Client.PostAsJsonAsync($"api/orders/{orderId}/shipments", new ShipOrder.Request("Ada"), CancellationToken);

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = JsonNode.Parse(await second.Content.ReadAsStringAsync(CancellationToken))!;
        ((string?)problem["detail"]).ShouldBe($"Order {orderId} has already been shipped.");
    }

    [Fact]
    public async Task Ship_unknown_order_returns_a_not_found_problem()
    {
        // Act
        using var response = await Client.PostAsJsonAsync($"api/orders/{Guid.NewGuid()}/shipments", new ShipOrder.Request("Ada"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShipmentDelivered_completes_the_fulfillment()
    {
        // Arrange
        var orderId = await PlaceOrderAsync();
        var fulfillment = await WaitForFulfillmentAsync(orderId);
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        // Act
        await publisher.PublishAsync(new ShipmentDelivered(Guid.NewGuid(), orderId, "acme-express", DateTimeOffset.UtcNow, 3), CancellationToken);

        // Assert
        var completed = await (await Client.GetAsync($"api/Fulfillments/{fulfillment.Id}", CancellationToken)).GetResponseAsync<FulfillmentDto>(CancellationToken);
        completed.Status.ShouldBeOfType<FulfillmentStatus.CompletedStatus>().ScanCount.ShouldBe(3);
    }

    [Fact]
    public async Task ShipmentDelivered_dated_in_the_future_is_skipped_by_the_consume_filter()
    {
        // Arrange
        var orderId = await PlaceOrderAsync();
        var fulfillment = await WaitForFulfillmentAsync(orderId);
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        // Act
        await publisher.PublishAsync(new ShipmentDelivered(Guid.NewGuid(), orderId, "acme-express", DateTimeOffset.UtcNow.AddDays(2), 3), CancellationToken);

        // Assert
        var unchanged = await (await Client.GetAsync($"api/Fulfillments/{fulfillment.Id}", CancellationToken)).GetResponseAsync<FulfillmentDto>(CancellationToken);
        unchanged.Status.ShouldBeOfType<FulfillmentStatus.InProgressStatus>();
    }

    private async Task<Guid> PlaceOrderAsync()
    {
        var result = await Sender.SendAsync(new PlaceOrderCommand(Guid.NewGuid().ToString()), CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<FulfillmentDto> WaitForFulfillmentAsync(Guid orderId)
    {
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(20), async () =>
        {
            var inProgress = await Sender.SendAsync(new GetInProgressFulfillmentsQuery(), CancellationToken);
            return inProgress.Bind(fulfillments => fulfillments.Find(s => s.OrderId == orderId)
                .ToResult(Error.NotFound("Fulfillment.NotFound", "The fulfillment does not exist yet.")));
        }, CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
