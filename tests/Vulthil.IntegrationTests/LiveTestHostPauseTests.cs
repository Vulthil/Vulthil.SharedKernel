using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Covers the live-host rule of <see cref="BaseWebApplicationFactory{TEntryPoint}"/>: while a test runs on its own
/// derived host, the class's shared host pauses its restartable hosted services, the reset leaves them paused, and they
/// resume once the derived host stops.
/// </summary>
public sealed class LiveTestHostPauseTests(RestartProbeWebApplicationFactory factory)
    : BaseIntegrationTestCase<Program>(factory), IClassFixture<RestartProbeWebApplicationFactory>
{
    private bool _disposedOnce;

    private RestartProbeWebApplicationFactory ProbeFactory => (RestartProbeWebApplicationFactory)FactoryFixture;

    [Fact]
    public async Task TheSharedHostPausesWhileATestRunsOnItsOwnHostAndResumesAfterTheTest()
    {
        // Arrange
        _ = FactoryFixture.Services;
        GetMock<IPublisher>();
        _ = Factory.Services;
        var whileTheTestHostRuns = ProbeFactory.Events.ToArray();

        // Act
        await DisposeAsync();

        // Assert
        whileTheTestHostRuns.ShouldBe(["start:host1", "stop:host1", "start:host2"]);
        ProbeFactory.Events.Where(e => e.EndsWith(":host1", StringComparison.Ordinal)).ShouldBe(["start:host1", "stop:host1", "start:host1"]);
        var testHostEvents = ProbeFactory.Events.Where(e => e.EndsWith(":host2", StringComparison.Ordinal)).ToArray();
        testHostEvents.Take(3).ShouldBe(["start:host2", "stop:host2", "start:host2"]);
        testHostEvents.Skip(3).ShouldNotBeEmpty();
        testHostEvents.Skip(3).ShouldAllBe(e => e == "stop:host2");
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
}
