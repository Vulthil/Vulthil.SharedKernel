using Vulthil.SharedKernel.Outbox;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// An <see cref="IOutboxSignal"/> that counts relay wake-ups. The provider host fixtures never start the relay, so
/// tests read how often a unit of work would have woken it.
/// </summary>
public sealed class CountingOutboxSignal : IOutboxSignal
{
    private int _notifyCount;

    /// <summary>Gets the number of wake-ups since the last <see cref="Reset"/>.</summary>
    public int NotifyCount => Volatile.Read(ref _notifyCount);

    /// <inheritdoc />
    public void Notify() => Interlocked.Increment(ref _notifyCount);

    /// <inheritdoc />
    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.Delay(timeout, cancellationToken);

    /// <summary>Clears the wake-up count.</summary>
    public void Reset() => Interlocked.Exchange(ref _notifyCount, 0);
}
