using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Extensions.Hosting;

namespace Vulthil.xUnit;

/// <summary>
/// Resets a test host between tests: stops every <see cref="IRestartableHostedService"/> the host runs, resets the
/// given resources, then restarts exactly the services it stopped. Each step runs on its own
/// <see cref="StepTimeout"/>-bounded token, never the test's — a test that timed out must still leave a clean fixture
/// behind for the next one. A failing step never skips the remaining steps, so a healthy service is always restarted
/// and a database is reset even when a service misbehaves; every failure is reported together, naming the offending
/// service or resource.
/// </summary>
/// <param name="timeProvider">The clock the per-step timeouts run on.</param>
internal sealed class TestHostReset(TimeProvider timeProvider)
{
    /// <summary>The bound on each stop, reset and restart step.</summary>
    internal static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs the stop, reset, restart sequence against <paramref name="hostServices"/>.
    /// </summary>
    /// <param name="hostServices">The service provider of the host the test ran against.</param>
    /// <param name="resources">The resources to reset while the restartable services are paused.</param>
    /// <returns>A task that completes when every step has run.</returns>
    /// <exception cref="AggregateException">One or more steps failed or timed out; every other step still ran.</exception>
    public async Task ResetAsync(IServiceProvider hostServices, IReadOnlyCollection<IResettableResource> resources)
    {
        var restartableServices = hostServices.GetServices<IHostedService>().OfType<IRestartableHostedService>().ToList();
        var failures = new List<Exception>();
        var stoppedServices = new List<IRestartableHostedService>();

        foreach (var service in restartableServices)
        {
            if (await TryRunStepAsync(service.StopAsync, $"Stopping '{service.GetType().Name}'", failures).ConfigureAwait(false))
            {
                stoppedServices.Add(service);
            }
        }

        var resetFailures = await Task.WhenAll(resources.Select(resource => TryResetAsync(resource, hostServices))).ConfigureAwait(false);
        failures.AddRange(resetFailures.OfType<Exception>());

        foreach (var service in stoppedServices)
        {
            await TryRunStepAsync(service.StartAsync, $"Restarting '{service.GetType().Name}'", failures).ConfigureAwait(false);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Resetting the test host after the test failed; every remaining step still ran. See the inner exceptions for the failing services and resources.",
                failures);
        }
    }

    private async Task<bool> TryRunStepAsync(Func<CancellationToken, Task> step, string description, List<Exception> failures)
    {
        using var timeout = new CancellationTokenSource(StepTimeout, timeProvider);
        try
        {
            // WaitAsync bounds a step that ignores its token: the step keeps running in the background, but the
            // reset moves on so a stuck service cannot block every later test.
            await step(timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            failures.Add(new TimeoutException($"{description} did not complete within {StepTimeout.TotalSeconds:0} seconds."));
            return false;
        }
        catch (Exception exception)
        {
            failures.Add(new InvalidOperationException($"{description} failed.", exception));
            return false;
        }
    }

    private static async Task<Exception?> TryResetAsync(IResettableResource resource, IServiceProvider hostServices)
    {
        try
        {
            await resource.ResetAsync(hostServices).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return new InvalidOperationException($"Resetting '{resource.GetType().Name}' failed.", exception);
        }
    }
}
