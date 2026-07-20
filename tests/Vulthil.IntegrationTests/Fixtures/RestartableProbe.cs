using System.Collections.Concurrent;
using Vulthil.Extensions.Hosting;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Restartable hosted service that records every start/stop into the given shared queue, so a test can verify
/// exactly which host instance a database reset paused and resumed — used to cover the fixture-vs-derived-factory
/// reset routing in <see cref="Vulthil.xUnit.BaseIntegrationTestCase{TFactory, TEntryPoint}"/>.
/// </summary>
/// <param name="events">The queue this instance's start/stop events are recorded into.</param>
internal sealed class RestartableProbe(ConcurrentQueue<string> events) : IRestartableHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        events.Enqueue("start");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        events.Enqueue("stop");
        return Task.CompletedTask;
    }
}
