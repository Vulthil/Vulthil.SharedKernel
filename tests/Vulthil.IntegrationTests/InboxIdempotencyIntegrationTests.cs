using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.TestHost.Probes;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

public sealed class InboxIdempotencyIntegrationTests(TestHarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<TestHarnessWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<TestHarnessWebApplicationFactory>
{
    private ITestHarness Harness => Factory.Services.GetRequiredService<ITestHarness>();
    private IPublisher Publisher => ScopedServices.GetRequiredService<IPublisher>();
    private IRequester Requester => Factory.Services.GetRequiredService<IRequester>();

    [Fact]
    public async Task RedeliveringTheSameEventProcessesTheConsumerExactlyOnce()
    {
        // Arrange — the inbox is configured to key on the probe id, so two publishes of the same id are duplicates.
        var probeId = Guid.NewGuid();
        var integrationEvent = new ProbeCreatedIntegrationEvent(probeId);

        // Act
        await Publisher.PublishAsync(integrationEvent, CancellationToken);
        await Publisher.PublishAsync(integrationEvent, CancellationToken);

        // Assert — both deliveries are captured, but the consumer runs (and writes its side effect) only once.
        Harness.Published<ProbeCreatedIntegrationEvent>().Count.ShouldBe(2);
        Harness.Consumed<ProbeCreatedIntegrationEvent>().ShouldHaveSingleItem().Message.Id.ShouldBe(probeId);

        var sideEffects = await Requester.RequestAsync<GetProbeSideEffects, List<ProbeSideEffectDto>>(
            new GetProbeSideEffects(probeId),
            CancellationToken);

        sideEffects.IsSuccess.ShouldBeTrue();
        sideEffects.Value.Count(sideEffect => sideEffect.ProbeId == probeId).ShouldBe(1);
    }
}
