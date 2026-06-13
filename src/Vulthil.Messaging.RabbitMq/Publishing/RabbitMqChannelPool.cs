using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Vulthil.Messaging.RabbitMq.Publishing;

/// <summary>
/// A bounded pool of publisher <see cref="IChannel"/> instances. Each lease hands out a channel that is used
/// non-concurrently (a RabbitMQ.Client v7 requirement); concurrency comes from leasing distinct channels, capped
/// at the configured size. Channels are created lazily with publisher confirms enabled and reused on return; a
/// channel that faults during publish is discarded rather than returned, so a poisoned channel is never reused.
/// </summary>
internal sealed class RabbitMqChannelPool : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IOptions<RabbitMqTransportOptions> _options;
    private readonly Lazy<SemaphoreSlim> _lazySemaphore;
    private readonly ConcurrentQueue<IChannel> _idle = new();
    private SemaphoreSlim Capacity => _lazySemaphore.Value;
    private bool _disposed;

    public RabbitMqChannelPool(IConnection connection, IOptions<RabbitMqTransportOptions> options)
    {
        _connection = connection;
        _options = options;
        _lazySemaphore = new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(_options.Value.PublishChannelPoolSize, _options.Value.PublishChannelPoolSize));
    }

    /// <summary>
    /// Leases a channel, waiting if the pool is at capacity. The caller must return it via <see cref="Return"/>
    /// (on success) or <see cref="DiscardAsync"/> (if the channel faulted) so the capacity slot is released.
    /// </summary>
    public async ValueTask<IChannel> LeaseAsync(CancellationToken cancellationToken)
    {
        await Capacity.WaitAsync(cancellationToken);
        try
        {
            if (_idle.TryDequeue(out var pooled))
            {
                return pooled;
            }

            return await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true,
                    consumerDispatchConcurrency: 1),
                cancellationToken);
        }
        catch
        {
            Capacity.Release();
            throw;
        }
    }

    /// <summary>Returns a healthy leased channel to the pool for reuse and frees its capacity slot.</summary>
    public void Return(IChannel channel)
    {
        _idle.Enqueue(channel);
        Capacity.Release();
    }

    /// <summary>Disposes a faulted leased channel instead of reusing it, freeing its capacity slot.</summary>
    public async ValueTask DiscardAsync(IChannel channel)
    {
        try
        {
            await channel.DisposeAsync();
        }
        finally
        {
            Capacity.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        while (_idle.TryDequeue(out var channel))
        {
            await channel.DisposeAsync();
        }

        if (_lazySemaphore.IsValueCreated)
        {
            Capacity.Dispose();
        }
    }
}
