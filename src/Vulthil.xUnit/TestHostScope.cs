using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Extensions.Hosting;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.xUnit;

/// <summary>
/// Owns the test-host scope of one factory: the scope views of the shared containers it consumes, the connection
/// strings and the start-up order of every test host built against them, the set of live hosts, the reset between
/// tests, and the disposal of the views.
/// </summary>
/// <remarks>
/// A test class's shared host and every per-test host derived from it use the same scope, and so the same databases,
/// virtual hosts and other namespaces. Only the newest live host runs its <see cref="IRestartableHostedService"/>s:
/// when a host starts, the other live hosts' restartable services are paused, and when the newest host stops, the
/// host before it resumes. So two hosts never run background work — a relay polling the database, consumers reading
/// the same queues — against the shared state at once.
/// </remarks>
internal sealed class TestHostScope : IAsyncDisposable
{
    private readonly ContainerHost _containerHost;
    private readonly Func<ITestContainer, bool> _shouldUseContainer;
    private readonly Func<string> _createScopeId;
    private readonly TestHostReset _reset;
    private readonly SemaphoreSlim _hostsGate = new(1, 1);
    private readonly List<LiveHost> _liveHosts = [];
    private List<ITestContainer> _containers = [];
    private bool _initialized;

    /// <summary>
    /// Initializes a scope over the containers of <paramref name="containerHost"/>.
    /// </summary>
    /// <param name="containerHost">The assembly-level host whose containers the scope consumes.</param>
    /// <param name="shouldUseContainer">Decides whether the scope consumes a host container.</param>
    /// <param name="createScopeId">Creates the identifier the scope's views are isolated under.</param>
    /// <param name="timeProvider">The clock the per-step timeouts of pauses and resets run on.</param>
    public TestHostScope(ContainerHost containerHost, Func<ITestContainer, bool> shouldUseContainer, Func<string> createScopeId, TimeProvider timeProvider)
    {
        _containerHost = containerHost;
        _shouldUseContainer = shouldUseContainer;
        _createScopeId = createScopeId;
        _reset = new TestHostReset(timeProvider);
    }

    /// <summary>
    /// Gets the scope views of the consumed containers, in the order the container host registered them.
    /// </summary>
    public IReadOnlyList<ITestContainer> Containers => _containers;

    /// <summary>
    /// Gets the configuration a test host needs to reach the consumed containers: one
    /// <c>ConnectionStrings:{key}</c> entry per container with a connection string.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> ConnectionStrings => _containers
        .OfType<ITestContainerWithConnectionString>()
        .Select(container => KeyValuePair.Create($"ConnectionStrings:{container.ConnectionStringKey}", container.ConnectionString));

    /// <summary>
    /// Starts the consumed host containers (once per test run, on the container host) and provisions this scope in
    /// each of them in parallel. Runs once; later calls do nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous startup work.</returns>
    /// <exception cref="InvalidOperationException">
    /// Two consumed containers use the same connection string key; the check runs before the scope's namespaces are
    /// provisioned.
    /// </exception>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        var consumedContainers = _containerHost.Containers.Where(_shouldUseContainer).ToList();
        await Parallel.ForEachAsync(consumedContainers, async (container, ct) => await _containerHost.EnsureStartedAsync(container).ConfigureAwait(false)).ConfigureAwait(false);

        var scopeId = _createScopeId();
#pragma warning disable CA2000 // Ownership transfers to _containers; scope views are disposed in DisposeAsync.
        var views = consumedContainers.Select(container => (Container: container, View: CreateScopeView(container, scopeId))).ToList();
#pragma warning restore CA2000
        ThrowOnSharedConnectionStringKey(views);
        _containers = [.. views.Select(pair => pair.View)];
        await Parallel.ForEachAsync(_containers, (container, ct) => container.InitializeAsync()).ConfigureAwait(false);
        _initialized = true;
    }

    /// <summary>
    /// Creates the hosted service that connects a test host to this scope. Register it first in the host, so its start
    /// runs before the application's own hosted services and its stop runs after them.
    /// </summary>
    /// <param name="hostServices">The root service provider of the host.</param>
    /// <returns>The hosted service to register.</returns>
    public IHostedService CreateHostLifecycle(IServiceProvider hostServices) => new HostLifecycle(this, hostServices);

    /// <summary>
    /// Resets the scope between tests: pauses the restartable services of every running live host, resets the scope's
    /// resettable views and <paramref name="additionalResources"/>, and resumes the services it paused. Does nothing
    /// when no host is live. Resources resolve application services from the newest live host — the one the test ran on.
    /// </summary>
    /// <param name="additionalResources">Resources the adapter owns, reset together with the scope's views.</param>
    /// <returns>A task that completes when every step has run.</returns>
    /// <exception cref="AggregateException">One or more steps failed or timed out; every other step still ran.</exception>
    public async Task ResetAsync(IReadOnlyCollection<IResettableResource> additionalResources)
    {
        await _hostsGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_liveHosts.Count == 0)
            {
                return;
            }

            var runningServices = _liveHosts
                .Where(host => !host.IsPaused)
                .SelectMany(host => TestHostReset.RestartableServicesOf(host.Services))
                .ToList();
            var resources = _containers.OfType<IResettableResource>().Concat(additionalResources).ToList();
            await _reset.ResetAsync(runningServices, resources, _liveHosts[^1].Services).ConfigureAwait(false);
        }
        finally
        {
            _hostsGate.Release();
        }
    }

    /// <summary>
    /// Disposes the scope views, which removes the scope's namespaces from the shared containers. Dispose the hosts
    /// first, so their graceful shutdown still has its infrastructure.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose work.</returns>
    public async ValueTask DisposeAsync()
    {
        await Parallel.ForEachAsync(_containers, (container, ct) => container.DisposeAsync()).ConfigureAwait(false);
        _hostsGate.Dispose();
    }

    /// <summary>
    /// Throws when two views would write the same connection string key, which the test host would otherwise resolve
    /// silently to one of them. Runs before the views provision their namespaces, so a misconfigured scope creates no
    /// database or virtual host.
    /// </summary>
    private static void ThrowOnSharedConnectionStringKey(IReadOnlyList<(ITestContainer Container, ITestContainer View)> views)
    {
        // Configuration keys are case-insensitive, so keys that differ only in case would still overwrite each other.
        var sharedKey = views
            .Where(pair => pair.View is ITestContainerWithConnectionString)
            .GroupBy(pair => ((ITestContainerWithConnectionString)pair.View).ConnectionStringKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Skip(1).Any());

        if (sharedKey is not null)
        {
            var containerNames = string.Join(", ", sharedKey.Select(pair => $"'{pair.Container.GetType().Name}'"));
            throw new InvalidOperationException(
                $"The containers {containerNames} all use the connection string key '{sharedKey.Key}', so only one of them would reach the test host. " +
                "Give each consumed container its own ConnectionStringKey, or exclude the extra ones with ShouldUseContainer.");
        }
    }

    private static ITestContainer CreateScopeView(ITestContainer container, string scopeId) => container switch
    {
        ITestContainerScopeProvider scopeProvider => scopeProvider.CreateScope(scopeId),
        ITestContainerWithConnectionString withConnectionString => new TestContainerWithConnectionStringScope(withConnectionString),
        _ => new TestContainerScope(container),
    };

    /// <summary>
    /// Registers a starting host: pauses the restartable services of every other running live host, applies pending
    /// database migrations, and runs the one-time startup resources, all before the host's own hosted services start.
    /// </summary>
    private async Task HostStartingAsync(IServiceProvider hostServices)
    {
        await _hostsGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_liveHosts.Exists(host => ReferenceEquals(host.Services, hostServices)))
            {
                return;
            }

            var failures = new List<Exception>();
            foreach (var host in _liveHosts.Where(host => !host.IsPaused))
            {
                host.PausedServices = await _reset.StopAsync(TestHostReset.RestartableServicesOf(host.Services), failures).ConfigureAwait(false);
            }

            // The host joins the live set even when a pause failed, so its stop still resumes the hosts it paused.
            _liveHosts.Add(new LiveHost(hostServices));
            TestHostReset.ThrowIfFailed(failures, "Pausing the other live test hosts before a new test host started failed.");

            await PrepareHostAsync(hostServices).ConfigureAwait(false);
        }
        finally
        {
            _hostsGate.Release();
        }
    }

    /// <summary>
    /// Removes a stopped host from the live set and resumes the newest remaining host when this host had paused it.
    /// </summary>
    private async Task HostStoppedAsync(IServiceProvider hostServices)
    {
        await _hostsGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_liveHosts.RemoveAll(host => ReferenceEquals(host.Services, hostServices)) == 0 || _liveHosts.Count == 0)
            {
                return;
            }

            var newest = _liveHosts[^1];
            if (newest.PausedServices is not { } pausedServices)
            {
                return;
            }

            newest.PausedServices = null;
            var failures = new List<Exception>();
            await _reset.StartAsync(pausedServices, failures).ConfigureAwait(false);
            TestHostReset.ThrowIfFailed(failures, "Resuming the paused test host after a newer test host stopped failed.");
        }
        finally
        {
            _hostsGate.Release();
        }
    }

    private async Task PrepareHostAsync(IServiceProvider hostServices)
    {
        var scope = hostServices.CreateAsyncScope();
        await using var _ = scope.ConfigureAwait(false);
        await Parallel.ForEachAsync(_containers.OfType<ITestDatabaseContainer>(), (container, ct) => container.MigrateDatabase(scope.ServiceProvider)).ConfigureAwait(false);
        await Parallel.ForEachAsync(_containers.OfType<IStartupResource>(), (resource, ct) => resource.InitializeAsync(hostServices)).ConfigureAwait(false);
    }

    /// <summary>
    /// A host built against the scope, with the restartable services the scope paused for a newer host, if any.
    /// </summary>
    private sealed class LiveHost(IServiceProvider services)
    {
        public IServiceProvider Services { get; } = services;

        public IReadOnlyList<IRestartableHostedService>? PausedServices { get; set; }

        public bool IsPaused => PausedServices is not null;
    }

    /// <summary>
    /// Connects one test host to the scope for the host's lifetime.
    /// </summary>
    private sealed class HostLifecycle(TestHostScope scope, IServiceProvider hostServices) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => scope.HostStartingAsync(hostServices);

        public Task StopAsync(CancellationToken cancellationToken) => scope.HostStoppedAsync(hostServices);
    }
}
