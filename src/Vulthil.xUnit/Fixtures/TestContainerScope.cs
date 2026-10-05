using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// A view of a shared test container for one scope: one consuming factory, so one test class. The view forwards the
/// host configuration to the shared container, creates the scope's namespace when it initializes, and deletes the
/// namespace when it is disposed; the shared container's own lifecycle is never affected, because a
/// <see cref="ContainerHost"/> owns it. Derive from this class to isolate a scope in a namespace of the containerized
/// service (a database, a virtual host, a key prefix): override <see cref="CreateNamespaceAsync"/> and
/// <see cref="DeleteNamespaceAsync"/>, and implement <see cref="IResettableResource"/> when the namespace can be reset
/// between tests.
/// </summary>
/// <remarks>
/// <para>
/// Deleting the namespace is best-effort: a failure is reported as a diagnostic message and never fails the test class,
/// because the namespace is removed with the container anyway.
/// </para>
/// <para>
/// The view never resets the shared container. A view whose namespace can be reset resets only that namespace, through
/// its own <see cref="IResettableResource"/> implementation, so one test class never clears the data of another.
/// </para>
/// </remarks>
/// <typeparam name="TContainer">The shared container the view belongs to.</typeparam>
public abstract class TestContainerScope<TContainer> : ITestContainer
    where TContainer : ITestContainer
{
    private readonly string? _namespaceName;

    /// <summary>
    /// Initializes the view.
    /// </summary>
    /// <param name="container">The shared container the view belongs to.</param>
    /// <param name="namespaceName">
    /// The name of the scope's namespace inside the shared container, or <see langword="null"/> when the view shares the
    /// container's own namespace and so has nothing to create or delete.
    /// </param>
    protected TestContainerScope(TContainer container, string? namespaceName)
    {
        ArgumentNullException.ThrowIfNull(container);

        Container = container;
        _namespaceName = namespaceName;
    }

    /// <summary>
    /// Gets the shared container the view belongs to.
    /// </summary>
    protected TContainer Container { get; }

    /// <inheritdoc />
    public virtual void ConfigureWebHost(IWebHostBuilder builder) => Container.ConfigureWebHost(builder);

    /// <inheritdoc />
    public virtual void ConfigureServices(IServiceCollection services) => Container.ConfigureServices(services);

    /// <summary>
    /// Creates the scope's namespace through <see cref="CreateNamespaceAsync"/>. Does nothing when the view shares the
    /// container's namespace.
    /// </summary>
    /// <returns>A task that completes when the namespace exists.</returns>
    public ValueTask InitializeAsync() =>
        _namespaceName is { } namespaceName ? CreateNamespaceAsync(namespaceName) : ValueTask.CompletedTask;

    /// <summary>
    /// Deletes the scope's namespace through <see cref="DeleteNamespaceAsync"/>, best-effort: a failure is reported as a
    /// diagnostic message instead of being thrown. Does nothing when the view shares the container's namespace.
    /// </summary>
    /// <returns>A task that completes when the delete has finished or failed.</returns>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_namespaceName is not { } namespaceName)
        {
            return;
        }

        try
        {
            await DeleteNamespaceAsync(namespaceName).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            TestContext.Current.SendDiagnosticMessage(
                $"Deleting the scope namespace '{namespaceName}' from '{Container.GetType().Name}' failed; it is removed with the container: {exception.Message}");
        }
    }

    /// <summary>
    /// Creates the scope's namespace inside the shared container, for example with <c>CREATE DATABASE</c>. Runs once,
    /// when the view initializes. The default does nothing.
    /// </summary>
    /// <param name="namespaceName">The name of the namespace to create.</param>
    /// <returns>A task that completes when the namespace exists.</returns>
    protected virtual ValueTask CreateNamespaceAsync(string namespaceName) => ValueTask.CompletedTask;

    /// <summary>
    /// Deletes the scope's namespace from the shared container. Runs once, when the view is disposed; an exception is
    /// reported as a diagnostic message instead of failing the test class. The default does nothing.
    /// </summary>
    /// <param name="namespaceName">The name of the namespace to delete.</param>
    /// <returns>A task that completes when the namespace is gone.</returns>
    protected virtual ValueTask DeleteNamespaceAsync(string namespaceName) => ValueTask.CompletedTask;
}

/// <summary>
/// A <see cref="TestContainerScope{TContainer}"/> of a shared container that has a connection string. The view
/// forwards the container's connection string key, and by default its connection string; a view whose namespace has
/// an address of its own (a database name, a virtual host) overrides <see cref="ConnectionString"/>.
/// </summary>
/// <typeparam name="TContainer">The shared container the view belongs to.</typeparam>
public abstract class TestContainerWithConnectionStringScope<TContainer>
    : TestContainerScope<TContainer>, ITestContainerWithConnectionString
    where TContainer : ITestContainerWithConnectionString
{
    /// <summary>
    /// Initializes the view.
    /// </summary>
    /// <param name="container">The shared container the view belongs to.</param>
    /// <param name="namespaceName">
    /// The name of the scope's namespace inside the shared container, or <see langword="null"/> when the view shares the
    /// container's own namespace and so has nothing to create or delete.
    /// </param>
    protected TestContainerWithConnectionStringScope(TContainer container, string? namespaceName)
        : base(container, namespaceName)
    {
    }

    /// <summary>
    /// Gets the connection string the scope's consumers use. The default is the shared container's connection string;
    /// override it when the namespace has an address of its own.
    /// </summary>
    public virtual string ConnectionString => Container.ConnectionString;

    /// <inheritdoc />
    public string ConnectionStringKey => Container.ConnectionStringKey;
}

/// <summary>
/// Pass-through scope view over a shared container that offers no per-scope isolation: the view shares the container's
/// namespace, and a reset is forwarded to the container. State inside the container is therefore shared by every test
/// class consuming it concurrently, so a stateful container should return a view of its own from
/// <see cref="ITestContainerScopeProvider.CreateScope"/> instead.
/// </summary>
/// <param name="container">The shared container to delegate to.</param>
internal sealed class SharedContainerScope(ITestContainer container)
    : TestContainerScope<ITestContainer>(container, namespaceName: null), IResettableResource
{
    /// <summary>
    /// Forwards the reset to the shared container when it is resettable; otherwise does nothing. The reset acts on
    /// shared state and therefore also affects other test classes using the container.
    /// </summary>
    /// <param name="serviceProvider">The application's root service provider, forwarded to the container.</param>
    /// <returns>A task representing the asynchronous reset work.</returns>
    public ValueTask ResetAsync(IServiceProvider serviceProvider) => SharedContainerReset.ResetAsync(Container, serviceProvider);
}

/// <summary>
/// Pass-through scope view that also forwards the shared container's connection string unchanged, so consumers share
/// the container's namespace; a reset is forwarded to the container.
/// </summary>
/// <param name="container">The shared container to delegate to.</param>
internal sealed class SharedContainerWithConnectionStringScope(ITestContainerWithConnectionString container)
    : TestContainerWithConnectionStringScope<ITestContainerWithConnectionString>(container, namespaceName: null), IResettableResource
{
    /// <summary>
    /// Forwards the reset to the shared container when it is resettable; otherwise does nothing. The reset acts on
    /// shared state and therefore also affects other test classes using the container.
    /// </summary>
    /// <param name="serviceProvider">The application's root service provider, forwarded to the container.</param>
    /// <returns>A task representing the asynchronous reset work.</returns>
    public ValueTask ResetAsync(IServiceProvider serviceProvider) => SharedContainerReset.ResetAsync(Container, serviceProvider);
}

/// <summary>
/// The reset rule of the pass-through views: the shared container is reset when it is resettable.
/// </summary>
internal static class SharedContainerReset
{
    public static ValueTask ResetAsync(ITestContainer container, IServiceProvider serviceProvider) =>
        container is IResettableResource resettable ? resettable.ResetAsync(serviceProvider) : ValueTask.CompletedTask;
}
