namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Implemented by test containers that can mint per-scope views of themselves, so a single running container
/// (typically owned by a <see cref="ContainerHost"/> assembly fixture) can serve many test classes in parallel
/// without sharing state. A scope usually maps to an isolated namespace inside the shared container — a uniquely
/// named database on a database server, a virtual host on a message broker, or a key prefix in a cache.
/// </summary>
public interface ITestContainerScopeProvider
{
    /// <summary>
    /// Creates a view of this container that is isolated under <paramref name="scopeId"/>. The returned view's
    /// <see cref="IAsyncLifetime.InitializeAsync"/> provisions the namespace (for example <c>CREATE DATABASE</c>)
    /// and its <see cref="IAsyncDisposable.DisposeAsync"/> removes it again; the underlying container's own
    /// lifecycle is never affected by the view.
    /// </summary>
    /// <param name="scopeId">A short, unique, lowercase identifier for the scope, safe to embed in names.</param>
    /// <returns>The scoped container view.</returns>
    ITestContainer CreateScope(string scopeId);
}
