using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.TestHost.Probes;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Covers the harness reset in <see cref="BaseIntegrationTestCase{TEntryPoint}"/>: the teardown after a test clears the
/// test harness of the host the test ran on — its captured messages and its stubs — so the next test of the class
/// starts clean.
/// </summary>
public sealed class TestHarnessResetIntegrationTests(TestHarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<TestHarnessWebApplicationFactory>
{
    private bool _disposedOnce;

    private ITestHarness Harness => Factory.Services.GetRequiredService<ITestHarness>();

    [Fact]
    public async Task TheTeardownAfterATestClearsTheHarnessCapturesAndStubs()
    {
        // Arrange
        var handledIds = new ConcurrentQueue<Guid>();
        Harness.Handle<ProbeCreatedIntegrationEvent>(context =>
        {
            handledIds.Enqueue(context.Message.Id);
            return Task.CompletedTask;
        });
        var beforeReset = Guid.NewGuid();
        var afterReset = Guid.NewGuid();
        await PublishAsync(new ProbeCreatedIntegrationEvent(beforeReset));

        // Act
        await DisposeAsync();
        await PublishAsync(new ProbeCreatedIntegrationEvent(afterReset));

        // Assert
        Harness.Published<ProbeCreatedIntegrationEvent>().ShouldHaveSingleItem().Message.Id.ShouldBe(afterReset);
        handledIds.ShouldBe([beforeReset]);
    }

    /// <inheritdoc />
    protected override async ValueTask Dispose()
    {
        if (_disposedOnce)
        {
            return;
        }

        _disposedOnce = true;
        await base.Dispose();
    }

    private async Task PublishAsync(ProbeCreatedIntegrationEvent message)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(message, CancellationToken);
    }
}
