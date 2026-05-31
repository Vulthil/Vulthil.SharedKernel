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
    /// Resets the resource to a clean state.
    /// </summary>
    /// <returns>A task representing the asynchronous reset work.</returns>
    ValueTask ResetAsync();
}
