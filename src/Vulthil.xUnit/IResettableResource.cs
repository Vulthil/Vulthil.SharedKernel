using System;

namespace Vulthil.xUnit;

/// <summary>
/// A test resource whose state can be reset to a clean baseline between tests &#8212; for example a database
/// reset via Respawn, or an HTTP mock whose configured responses and captured requests are cleared.
/// </summary>
/// <remarks>
/// <see cref="BaseIntegrationTestCase{TFactory, TEntryPoint}"/> resets every registered resettable resource after each test.
/// </remarks>
public interface IResettableResource
{
    /// <summary>
    /// Resets the resource to a clean state after each test, with the application's root service provider so a resource
    /// that needs the app's services — for example a Cosmos store recreating its database through the registered
    /// <c>DbContext</c> — can resolve them from a fresh scope. Resources that need no services ignore the argument.
    /// One-time setup that must run before the first test instead belongs in <see cref="IStartupResource"/>.
    /// </summary>
    /// <param name="serviceProvider">The application's root service provider; create a scope from it to resolve scoped services.</param>
    /// <returns>A task representing the asynchronous reset work.</returns>
    ValueTask ResetAsync(IServiceProvider serviceProvider);
}
