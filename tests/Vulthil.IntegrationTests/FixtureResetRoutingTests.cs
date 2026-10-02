using Microsoft.AspNetCore.Mvc.Testing;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Covers the reset routing in <see cref="BaseIntegrationTestCase{TEntryPoint}"/>: overriding <c>CreateFactory()</c>
/// to run a test on a <c>WithWebHostBuilder(...)</c>-derived factory must reset that derived host, never the class
/// fixture's own (separate, otherwise-unused) host.
/// </summary>
public sealed class FixtureResetRoutingTests(RestartProbeWebApplicationFactory factory)
    : BaseIntegrationTestCase<Program>(factory), IClassFixture<RestartProbeWebApplicationFactory>
{
    private bool _disposedOnce;

    private RestartProbeWebApplicationFactory ProbeFactory => (RestartProbeWebApplicationFactory)FactoryFixture;

    protected override WebApplicationFactory<Program> CreateFactory() => FactoryFixture.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task ResettingAfterATestPausesAndResumesOnlyTheHostTheTestActuallyUsed()
    {
        // Arrange — forces the derived (WithWebHostBuilder) host to build and start; ASP.NET Core auto-starts its
        // registered IHostedServices, including this factory's RestartableProbe.
        _ = Factory.Services;

        // Act — runs the same reset dance xUnit would run automatically at the end of this test.
        await DisposeAsync();

        // Assert — the reset ran on the one host this test built: an initial auto-start, then the reset's own
        // stop/start pair; later events come from the derived factory's teardown. Every event belongs to that host, so
        // resetting never builds FactoryFixture's own, otherwise-unused host.
        var events = ProbeFactory.Events.ToArray();
        events.Take(3).ShouldBe(["start:host1", "stop:host1", "start:host1"]);
        events.ShouldAllBe(e => e.EndsWith(":host1", StringComparison.Ordinal));
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
