using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Pass-through scope view over a shared <see cref="ITestContainer"/>: host configuration and resets are forwarded to
/// the underlying container while the lifecycle is a no-op, because the container itself is owned by a
/// <see cref="ContainerHost"/>. Used when a container offers no per-scope isolation; state inside the container is
/// then shared by every test class consuming it concurrently, so stateful containers should provide a real scope via
/// <see cref="ITestContainerScopeProvider"/> instead.
/// </summary>
/// <typeparam name="TContainer">The container abstraction the scope delegates to.</typeparam>
/// <param name="container">The shared container to delegate to.</param>
internal class TestContainerScope<TContainer>(TContainer container) : ITestContainer, IResettableResource
    where TContainer : ITestContainer
{
    /// <summary>
    /// Gets the shared container this scope delegates to.
    /// </summary>
    protected TContainer Container { get; } = container;

    /// <summary>
    /// No-op: the underlying container is started by its owning <see cref="ContainerHost"/>.
    /// </summary>
    /// <returns>A completed task.</returns>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// No-op: the underlying container is disposed by its owning <see cref="ContainerHost"/>.
    /// </summary>
    /// <returns>A completed task.</returns>
    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual void ConfigureWebHost(IWebHostBuilder builder) => Container.ConfigureWebHost(builder);

    /// <inheritdoc />
    public virtual void ConfigureServices(IServiceCollection services) => Container.ConfigureServices(services);

    /// <summary>
    /// Forwards the reset to the underlying container when it is resettable; otherwise does nothing. Note that a
    /// forwarded reset acts on shared state and therefore also affects other test classes using the container.
    /// </summary>
    /// <returns>A task representing the asynchronous reset work.</returns>
    public virtual ValueTask ResetAsync() =>
        Container is IResettableResource resettable ? resettable.ResetAsync() : ValueTask.CompletedTask;
}

/// <summary>
/// Non-generic pass-through scope view over a shared <see cref="ITestContainer"/>.
/// </summary>
/// <param name="container">The shared container to delegate to.</param>
internal sealed class TestContainerScope(ITestContainer container) : TestContainerScope<ITestContainer>(container);

/// <summary>
/// Pass-through scope view that additionally exposes the shared container's connection string. The connection string
/// is forwarded unchanged, so consuming factories share the container's namespace; override
/// <see cref="ConnectionString"/> in a derived scope to point consumers at a per-scope namespace instead.
/// </summary>
/// <param name="container">The shared container to delegate to.</param>
internal sealed class TestContainerWithConnectionStringScope(ITestContainerWithConnectionString container)
    : TestContainerScope<ITestContainerWithConnectionString>(container), ITestContainerWithConnectionString
{
    /// <inheritdoc />
    public string ConnectionString => Container.ConnectionString;

    /// <inheritdoc />
    public string ConnectionStringKey => Container.ConnectionStringKey;
}
