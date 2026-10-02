using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Extensions.Hosting;

namespace Vulthil.xUnit;

/// <summary>
/// Stops and starts the <see cref="IRestartableHostedService"/>s of test hosts, and resets a test host between tests:
/// stops the given services, resets the given resources, then restarts exactly the services it stopped. Each step runs
/// on its own <see cref="StepTimeout"/>-bounded token, never the test's — a test that timed out must still leave a
/// clean fixture behind for the next one. A failing step never skips the remaining steps, so a healthy service is
/// always restarted and a database is reset even when a service misbehaves; every failure is reported together,
/// naming the offending service or resource.
/// </summary>
/// <param name="timeProvider">The clock the per-step timeouts run on.</param>
internal sealed class TestHostReset(TimeProvider timeProvider)
{
    /// <summary>The bound on each stop, reset and restart step.</summary>
    internal static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns the restartable hosted services of the host that owns <paramref name="hostServices"/>.
    /// </summary>
    /// <param name="hostServices">The root service provider of a test host.</param>
    /// <returns>The host's restartable hosted services, in registration order.</returns>
    public static IReadOnlyList<IRestartableHostedService> RestartableServicesOf(IServiceProvider hostServices) =>
        [.. hostServices.GetServices<IHostedService>().OfType<IRestartableHostedService>()];

    /// <summary>
    /// Runs the stop, reset, restart sequence.
    /// </summary>
    /// <param name="services">The services to pause while the resources are reset.</param>
    /// <param name="resources">The resources to reset while the services are paused.</param>
    /// <param name="resourceServices">The service provider the resources resolve application services from.</param>
    /// <returns>A task that completes when every step has run.</returns>
    /// <exception cref="AggregateException">One or more steps failed or timed out; every other step still ran.</exception>
    public async Task ResetAsync(
        IReadOnlyCollection<IRestartableHostedService> services,
        IReadOnlyCollection<IResettableResource> resources,
        IServiceProvider resourceServices)
    {
        var failures = new List<Exception>();
        var stoppedServices = await StopAsync(services, failures).ConfigureAwait(false);

        var resetFailures = await Task.WhenAll(resources.Select(resource => TryResetAsync(resource, resourceServices))).ConfigureAwait(false);
        failures.AddRange(resetFailures.OfType<Exception>());

        await StartAsync(stoppedServices, failures).ConfigureAwait(false);

        ThrowIfFailed(failures, "Resetting the test host after the test failed; every remaining step still ran.");
    }

    /// <summary>
    /// Stops every service in turn and returns the services that stopped; a stop that fails or times out is added to
    /// <paramref name="failures"/> and the next service is still stopped.
    /// </summary>
    /// <param name="services">The services to stop.</param>
    /// <param name="failures">Collects the failed and timed-out stops.</param>
    /// <returns>The services that stopped, in the order they stopped.</returns>
    public async Task<IReadOnlyList<IRestartableHostedService>> StopAsync(IEnumerable<IRestartableHostedService> services, List<Exception> failures)
    {
        var stoppedServices = new List<IRestartableHostedService>();
        foreach (var service in services)
        {
            if (await TryRunStepAsync(service.StopAsync, $"Stopping '{service.GetType().Name}'", failures).ConfigureAwait(false))
            {
                stoppedServices.Add(service);
            }
        }

        return stoppedServices;
    }

    /// <summary>
    /// Starts every service in turn; a start that fails or times out is added to <paramref name="failures"/> and the
    /// next service is still started.
    /// </summary>
    /// <param name="services">The services to start.</param>
    /// <param name="failures">Collects the failed and timed-out starts.</param>
    /// <returns>A task that completes when every service has been started.</returns>
    public async Task StartAsync(IEnumerable<IRestartableHostedService> services, List<Exception> failures)
    {
        foreach (var service in services)
        {
            await TryRunStepAsync(service.StartAsync, $"Restarting '{service.GetType().Name}'", failures).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Throws an <see cref="AggregateException"/> with <paramref name="message"/> when <paramref name="failures"/>
    /// holds any failure.
    /// </summary>
    /// <param name="failures">The failures collected by the steps.</param>
    /// <param name="message">What failed, for the exception message.</param>
    /// <exception cref="AggregateException"><paramref name="failures"/> is not empty.</exception>
    public static void ThrowIfFailed(List<Exception> failures, string message)
    {
        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"{message} See the inner exceptions for the failing services and resources.",
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

    private static async Task<Exception?> TryResetAsync(IResettableResource resource, IServiceProvider resourceServices)
    {
        try
        {
            await resource.ResetAsync(resourceServices).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return new InvalidOperationException($"Resetting '{resource.GetType().Name}' failed.", exception);
        }
    }
}
