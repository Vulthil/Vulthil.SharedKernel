using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.xUnit;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.Create;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class TestHarnessIntegrationTests(TestHarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<TestHarnessWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<TestHarnessWebApplicationFactory>
{
    private ITestHarness Harness => Factory.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => Factory.Services.GetRequiredService<IPublisher>();
    private IRequester Requester => Factory.Services.GetRequiredService<IRequester>();

    [Fact]
    public async Task PublishingAnIntegrationEventRunsTheRealConsumerThroughTheInMemoryHarness()
    {
        // Arrange
        var mainEntityId = Guid.NewGuid();

        // Act — the harness dispatches synchronously, with no broker.
        await Publisher.PublishAsync(new MainEntityCreatedIntegrationEvent(mainEntityId), CancellationToken);

        // Assert — the event was captured and the real consumer ran (no polling needed).
        Harness.Published<MainEntityCreatedIntegrationEvent>().ShouldHaveSingleItem().Message.Id.ShouldBe(mainEntityId);
        Harness.Consumed<MainEntityCreatedIntegrationEvent>().ShouldHaveSingleItem().Message.Id.ShouldBe(mainEntityId);

        // The consumer wrote a side effect to the real database; read it back through the request consumer.
        var sideEffects = await Requester.RequestAsync<GetSideEffectsBelongingToMainEntity, List<SideEffectDto>>(
            new GetSideEffectsBelongingToMainEntity(mainEntityId),
            CancellationToken);

        sideEffects.IsSuccess.ShouldBeTrue();
        sideEffects.Value.ShouldContain(sideEffect => sideEffect.MainEntityId == mainEntityId);
    }
}
