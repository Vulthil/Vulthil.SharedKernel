using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.TestHost.Probes;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

public sealed class TestHarnessIntegrationTests(TestHarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<TestHarnessWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<TestHarnessWebApplicationFactory>
{
    private ITestHarness Harness => Factory.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => ScopedServices.GetRequiredService<IPublisher>();
    private IRequester Requester => Factory.Services.GetRequiredService<IRequester>();

    [Fact]
    public async Task PublishingAnIntegrationEventRunsTheRealConsumerThroughTheInMemoryHarness()
    {
        // Arrange
        var probeId = Guid.NewGuid();

        // Act — the harness dispatches synchronously, with no broker.
        await Publisher.PublishAsync(new ProbeCreatedIntegrationEvent(probeId), CancellationToken);

        // Assert — the event was captured and the real consumer ran (no polling needed).
        Harness.Published<ProbeCreatedIntegrationEvent>().ShouldHaveSingleItem().Message.Id.ShouldBe(probeId);
        Harness.Consumed<ProbeCreatedIntegrationEvent>().ShouldHaveSingleItem().Message.Id.ShouldBe(probeId);

        // The consumer wrote a side effect to the real database; read it back through the request consumer.
        var sideEffects = await Requester.RequestAsync<GetProbeSideEffects, List<ProbeSideEffectDto>>(
            new GetProbeSideEffects(probeId),
            CancellationToken);

        sideEffects.IsSuccess.ShouldBeTrue();
        sideEffects.Value.ShouldContain(sideEffect => sideEffect.ProbeId == probeId);
    }
}
