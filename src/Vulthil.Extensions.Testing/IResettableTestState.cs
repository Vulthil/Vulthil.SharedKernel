namespace Vulthil.Extensions.Testing;

/// <summary>
/// State that a test double keeps in a test host's services — captured messages, registered stubs, an in-memory
/// store — and that is reset to a clean baseline after each test, so tests that share one host never see each other's
/// state.
/// </summary>
/// <remarks>
/// <para>
/// Register the implementation in the test host's services as an <see cref="IResettableTestState"/>, usually as a
/// forward to the singleton that holds the state:
/// <code>
/// services.AddSingleton&lt;InMemoryEmailSender&gt;();
/// services.AddSingleton&lt;IEmailSender&gt;(sp =&gt; sp.GetRequiredService&lt;InMemoryEmailSender&gt;());
/// services.AddSingleton&lt;IResettableTestState&gt;(sp =&gt; sp.GetRequiredService&lt;InMemoryEmailSender&gt;());
/// </code>
/// </para>
/// <para>
/// A test framework adapter resets every registered instance after each test. <c>Vulthil.xUnit</c> resets the
/// instances of every live test host while the hosts' restartable background services are paused, and cancels the
/// token when a reset takes too long. With no such adapter, resolve the instances from the host's services in the
/// per-test teardown and call <see cref="ResetAsync"/> on each.
/// </para>
/// <para>
/// The reset runs after each test, not before it: what the host records while it starts, or while its background
/// services restart after a reset, is visible to the next test.
/// </para>
/// </remarks>
public interface IResettableTestState
{
    /// <summary>
    /// Resets the state to its clean baseline.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the reset must stop, for example because it took too long.</param>
    /// <returns>A task that completes when the state is reset.</returns>
    ValueTask ResetAsync(CancellationToken cancellationToken = default);
}
