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

public sealed class InboxIdempotencyIntegrationTests(TestHarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<TestHarnessWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<TestHarnessWebApplicationFactory>
{
    private ITestHarness Harness => Factory.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => ScopedServices.GetRequiredService<IPublisher>();
    private IRequester Requester => Factory.Services.GetRequiredService<IRequester>();

    [Fact]
    public async Task RedeliveringTheSameEventProcessesTheConsumerExactlyOnce()
    {
        // Arrange — the inbox is configured to key on the entity id, so two publishes of the same id are duplicates.
        var mainEntityId = Guid.NewGuid();
        var integrationEvent = new MainEntityCreatedIntegrationEvent(mainEntityId);

        // Act
        await Publisher.PublishAsync(integrationEvent, CancellationToken);
        await Publisher.PublishAsync(integrationEvent, CancellationToken);

        // Assert — both deliveries are captured, but the consumer runs (and writes its side effect) only once.
        Harness.Published<MainEntityCreatedIntegrationEvent>().Count.ShouldBe(2);
        Harness.Consumed<MainEntityCreatedIntegrationEvent>().ShouldHaveSingleItem().Message.Id.ShouldBe(mainEntityId);

        var sideEffects = await Requester.RequestAsync<GetSideEffectsBelongingToMainEntity, List<SideEffectDto>>(
            new GetSideEffectsBelongingToMainEntity(mainEntityId),
            CancellationToken);

        sideEffects.IsSuccess.ShouldBeTrue();
        sideEffects.Value.Count(sideEffect => sideEffect.MainEntityId == mainEntityId).ShouldBe(1);
    }
}
