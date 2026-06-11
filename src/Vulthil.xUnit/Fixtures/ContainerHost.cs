using System.Collections.Concurrent;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Assembly-level fixture that owns a set of shared test containers, so heavyweight infrastructure (database servers,
/// message brokers, emulators) starts at most once per test run instead of once per test class.
/// </summary>
/// <remarks>
/// <para>
/// Register a derived host with <c>[assembly: AssemblyFixture(typeof(MyContainerHost))]</c> and accept it in the
/// constructor of a <see cref="BaseWebApplicationFactory{TEntryPoint}"/>-derived class fixture. The factory consumes
/// every container registered here through a per-factory scope view (see <see cref="ITestContainerScopeProvider"/>),
/// so parallel test classes share the running containers without sharing state.
/// </para>
/// <para>
/// Containers start lazily on first use and concurrent consumers share a single startup task, so a filtered test run
/// only pays for the containers its factories actually consume. Assembly fixtures are used from many tests
/// simultaneously; this host is safe for that concurrency.
/// </para>
/// </remarks>
/// <param name="messageSink">The xUnit diagnostic message sink, forwarded to container fixtures.</param>
public abstract class ContainerHost(IMessageSink messageSink) : IAsyncLifetime
{
    private readonly HashSet<ITestContainer> _containers = [];
    private readonly ConcurrentDictionary<ITestContainer, Lazy<Task>> _startedContainers = new();
    private bool _configured;

    /// <summary>
    /// Gets the xUnit diagnostic message sink, for passing to container fixtures created by the host.
    /// </summary>
    protected IMessageSink MessageSink { get; } = messageSink;

    /// <summary>
    /// Gets the containers registered on this host.
    /// </summary>
    public IReadOnlyCollection<ITestContainer> Containers => _containers;

    /// <summary>
    /// Registers a shared test container on this host. Call from the constructor or from
    /// <see cref="ConfigureContainers"/>.
    /// </summary>
    /// <param name="container">The container to register.</param>
    protected void AddContainer(ITestContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        _containers.Add(container);
    }

    /// <summary>
    /// Override to register shared containers by calling <see cref="AddContainer"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous registration work.</returns>
    protected virtual Task ConfigureContainers() => Task.CompletedTask;

    /// <summary>
    /// Registers the configured containers. Invoked once by xUnit before the fixture is handed to its first consumer;
    /// containers themselves start lazily through <see cref="EnsureStartedAsync"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous registration work.</returns>
    public async ValueTask InitializeAsync()
    {
        if (_configured)
        {
            return;
        }

        await ConfigureContainers();
        _configured = true;
    }

    /// <summary>
    /// Starts the given container if it has not been started yet. Thread-safe: concurrent callers share a single
    /// startup task, and a failed startup is observed by every consumer instead of being retried.
    /// </summary>
    /// <param name="container">A container registered on this host.</param>
    /// <returns>A task that completes when the container is running.</returns>
    /// <exception cref="InvalidOperationException">The container is not registered on this host.</exception>
    public Task EnsureStartedAsync(ITestContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (!_containers.Contains(container))
        {
            throw new InvalidOperationException(
                $"Container '{container.GetType().Name}' is not registered on this host. Register it via AddContainer.");
        }

        return _startedContainers
            .GetOrAdd(container, static c => new Lazy<Task>(() => c.InitializeAsync().AsTask()))
            .Value;
    }

    /// <summary>
    /// Disposes every container that was started. Invoked once by xUnit after the last test in the assembly.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose work.</returns>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await Parallel.ForEachAsync(
            _startedContainers.Where(pair => pair.Value.IsValueCreated),
            async (pair, _) =>
            {
                try
                {
                    await pair.Value.Value.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    TestContext.Current.SendDiagnosticMessage(
                        $"Container '{pair.Key.GetType().Name}' failed during startup; disposing it anyway: {exception.Message}");
                }

                await pair.Key.DisposeAsync().ConfigureAwait(false);
            });
    }
}
