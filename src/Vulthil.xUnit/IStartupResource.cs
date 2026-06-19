using System;

namespace Vulthil.xUnit;

/// <summary>
/// A test resource that performs one-time setup during host startup using the application's services &#8212; for
/// example a Cosmos store creating its database and containers through the registered <c>DbContext</c>. Invoked once,
/// after database migrations and before the application's background services, so the resource is ready before the
/// first test runs.
/// </summary>
/// <remarks>
/// Distinct from <see cref="IResettableResource"/>: startup setup runs once before any test, whereas a reset runs after
/// each test. A resource may implement both &#8212; creating its store at startup and recreating it between tests.
/// </remarks>
public interface IStartupResource
{
    /// <summary>
    /// Performs the resource's one-time startup setup.
    /// </summary>
    /// <param name="serviceProvider">The application's root service provider; create a scope from it to resolve scoped services.</param>
    /// <returns>A task representing the asynchronous setup work.</returns>
    ValueTask InitializeAsync(IServiceProvider serviceProvider);
}
