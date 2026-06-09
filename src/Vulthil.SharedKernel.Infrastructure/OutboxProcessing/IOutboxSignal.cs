namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// Wakes the outbox background service when a transaction commits, so freshly-written outbox messages are relayed
/// promptly instead of waiting for the next poll. Signals coalesce (at most one pending wake), and the periodic
/// poll remains the correctness backstop for retries, other instances, and missed signals.
/// </summary>
public interface IOutboxSignal
{
    /// <summary>Signals that outbox work may be available, releasing a waiter if one is pending.</summary>
    void Notify();

    /// <summary>Waits until <see cref="Notify"/> is called or <paramref name="timeout"/> elapses.</summary>
    /// <param name="timeout">The maximum time to wait before returning regardless of a signal.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>
/// Default <see cref="IOutboxSignal"/> backed by a single-slot semaphore, so multiple commits between polls collapse
/// into one wake.
/// </summary>
internal sealed class OutboxSignal : IOutboxSignal, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake is already pending; signals coalesce.
        }
    }

    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _semaphore.WaitAsync(timeout, cancellationToken);

    public void Dispose() => _semaphore.Dispose();
}
