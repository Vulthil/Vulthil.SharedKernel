using System.Collections.Concurrent;
using Vulthil.Extensions.Hosting;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Restartable hosted service that records every start/stop into the given shared queue, tagged with the host it runs
/// in, so a test can verify exactly which host instance a reset or a pause stopped and started.
/// </summary>
/// <param name="events">The queue this instance's start/stop events are recorded into.</param>
/// <param name="host">The tag of the host this instance runs in, such as <c>host1</c>.</param>
internal sealed class RestartableProbe(ConcurrentQueue<string> events, string host) : IRestartableHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        events.Enqueue($"start:{host}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        events.Enqueue($"stop:{host}");
        return Task.CompletedTask;
    }
}
