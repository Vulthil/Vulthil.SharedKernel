using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.xUnit;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.Shipping;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.GetInProgress;
using WebApi.Domain.SideEffects;
using WebApi.MainEntity;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class ShippingTests(HarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<HarnessWebApplicationFactory>
{
    private ISender Sender => ScopedServices.GetRequiredService<ISender>();

    private ITestHarness Harness => ScopedServices.GetRequiredService<ITestHarness>();

    [Fact]
    public async Task Ship_sends_CreateShipment_to_the_shipments_queue_once_the_command_commits()
    {
        // Arrange
        var mainEntityId = await CreateMainEntityAsync();

        // Act
        using var response = await Client.PostAsJsonAsync($"api/main-entities/{mainEntityId}/shipments", new Ship.Request("Ada Lovelace"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var relayed = await Polling.WaitAsync(
            new PollingOptions(TimeSpan.FromSeconds(20)) { TimerTick = TimeSpan.FromMilliseconds(250) },
            _ => Task.FromResult(Harness.Sent<CreateShipment>().Any(sent => sent.Message.OrderReference == mainEntityId)),
            CancellationToken);
        relayed.IsSuccess.ShouldBeTrue();

        var sent = Harness.Sent<CreateShipment>().Single(captured => captured.Message.OrderReference == mainEntityId);
        sent.DestinationAddress.ShouldBe(new Uri("queue:shipment-commands"));
        sent.Envelope.MessageType.ShouldBe(new Uri(CreateShipment.Urn));
        sent.Envelope.Headers!["source-service"]!.ToString().ShouldBe("WebApi");
    }

    [Fact]
    public async Task Ship_unknown_entity_returns_a_not_found_problem()
    {
        // Act
        using var response = await Client.PostAsJsonAsync($"api/main-entities/{Guid.NewGuid()}/shipments", new Ship.Request("Ada"), CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShipmentDelivered_completes_the_side_effect()
    {
        // Arrange
        var mainEntityId = await CreateMainEntityAsync();
        var sideEffect = await WaitForSideEffectAsync(mainEntityId);
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        // Act
        await publisher.PublishAsync(new ShipmentDelivered(Guid.NewGuid(), mainEntityId, "acme-express", DateTimeOffset.UtcNow, 3), CancellationToken);

        // Assert
        var completed = await (await Client.GetAsync($"api/SideEffects/{sideEffect.Id}", CancellationToken)).GetResponseAsync<SideEffectDto>(CancellationToken);
        completed.Status.ShouldBeOfType<Status.CompletedStatus>().Value.ShouldBe(3);
    }

    [Fact]
    public async Task ShipmentDelivered_dated_in_the_future_is_skipped_by_the_consume_filter()
    {
        // Arrange
        var mainEntityId = await CreateMainEntityAsync();
        var sideEffect = await WaitForSideEffectAsync(mainEntityId);
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        // Act
        await publisher.PublishAsync(new ShipmentDelivered(Guid.NewGuid(), mainEntityId, "acme-express", DateTimeOffset.UtcNow.AddDays(2), 3), CancellationToken);

        // Assert
        var unchanged = await (await Client.GetAsync($"api/SideEffects/{sideEffect.Id}", CancellationToken)).GetResponseAsync<SideEffectDto>(CancellationToken);
        unchanged.Status.ShouldBeOfType<Status.InProgressStatus>();
    }

    private async Task<Guid> CreateMainEntityAsync()
    {
        var result = await Sender.SendAsync(new CreateMainEntityCommand(Guid.NewGuid().ToString()), CancellationToken);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<SideEffectDto> WaitForSideEffectAsync(Guid mainEntityId)
    {
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(20), async () =>
        {
            var inProgress = await Sender.SendAsync(new GetInProgressQuery(), CancellationToken);
            return inProgress.Bind(sideEffects => sideEffects.Find(s => s.MainEntityId == mainEntityId)
                .ToResult(Error.NotFound("SideEffect.NotFound", "The side effect does not exist yet.")));
        }, CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
