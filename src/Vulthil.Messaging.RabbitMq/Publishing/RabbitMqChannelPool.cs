using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq.Publishing;

/// <summary>
/// A bounded pool of publisher <see cref="IChannel"/> instances. Each lease hands out a channel that is used
/// non-concurrently (a RabbitMQ.Client v7 requirement); concurrency comes from leasing distinct channels, capped
/// at the configured size. Channels are created lazily with publisher confirms enabled and reused on return; a
/// channel that faults during publish is discarded rather than returned, so a poisoned channel is never reused.
/// A publish can still hold a lease when the pool is disposed at shutdown: the pool then closes that channel when it
/// comes back, instead of keeping it or failing the publish that already succeeded.
/// </summary>
internal sealed class RabbitMqChannelPool : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IOptions<RabbitMqTransportOptions> _options;
    private readonly Lazy<SemaphoreSlim> _lazySemaphore;
    private readonly ConcurrentQueue<IChannel> _idle = new();

    // Serializes the disposed flag with every capacity release and every enqueue, so no slot is released on the
    // disposed semaphore and no returned channel lands in the idle queue after DisposeAsync has emptied it.
    private readonly Lock _sync = new();

    private SemaphoreSlim Capacity => _lazySemaphore.Value;
    private bool _disposed;

    public RabbitMqChannelPool(IConnection connection, IOptions<RabbitMqTransportOptions> options)
    {
        _connection = connection;
        _options = options;
        _lazySemaphore = new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(_options.Value.PublishChannelPoolSize, _options.Value.PublishChannelPoolSize));
    }

    /// <summary>
    /// Leases a channel, waiting if the pool is at capacity. The caller must return it via <see cref="ReturnAsync"/>
    /// (on success) or <see cref="DiscardAsync"/> (if the channel faulted) so the capacity slot is released.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The pool is disposed.</exception>
    public async ValueTask<IChannel> LeaseAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);
        await Capacity.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);
            if (_idle.TryDequeue(out var pooled))
            {
                return pooled;
            }

            return await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true,
                    consumerDispatchConcurrency: 1),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseSlot();
            throw;
        }
    }

    /// <summary>
    /// Returns a healthy leased channel to the pool for reuse and frees its capacity slot. After the pool is disposed,
    /// it closes the channel instead.
    /// </summary>
    public ValueTask ReturnAsync(IChannel channel)
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _idle.Enqueue(channel);
                Capacity.Release();
                return ValueTask.CompletedTask;
            }
        }

        return channel.DisposeAsync();
    }

    /// <summary>Disposes a faulted leased channel instead of reusing it, freeing its capacity slot.</summary>
    public async ValueTask DiscardAsync(IChannel channel)
    {
        try
        {
            await channel.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            ReleaseSlot();
        }
    }

    private void ReleaseSlot()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                Capacity.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        while (_idle.TryDequeue(out var channel))
        {
            await channel.DisposeAsync().ConfigureAwait(false);
        }

        if (_lazySemaphore.IsValueCreated)
        {
            Capacity.Dispose();
        }
    }
}
