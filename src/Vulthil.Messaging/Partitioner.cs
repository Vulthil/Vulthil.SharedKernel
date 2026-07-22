using System.Buffers;
using System.Text;

namespace Vulthil.Messaging;

/// <summary>
/// Distributes work across a fixed number of ordered partitions ("lanes"), selected by a hash of a
/// partition key. Work submitted for the same key runs strictly sequentially and in submission order;
/// work for keys that hash to different lanes runs concurrently.
/// </summary>
/// <remarks>
/// Used on the consume side to give per-aggregate ordering — messages correlated to the same aggregate
/// are processed one at a time and in order, while unrelated messages still process in parallel. A single
/// instance can be shared across multiple message types so that messages correlated to the same key are
/// serialized together regardless of their type. The hash is in-process only, so the lane assignment for a
/// given key need not be stable across processes.
/// </remarks>
public sealed class Partitioner
{
    private readonly object[] _gates;
    private readonly Task[] _tails;

    /// <summary>
    /// Gets the number of partitions (lanes) this partitioner distributes work across.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Initializes a new <see cref="Partitioner"/> with the specified number of partitions.
    /// </summary>
    /// <param name="partitionCount">
    /// The number of lanes. A larger count lets more distinct keys make progress concurrently; the count
    /// affects only fan-out, never correctness.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="partitionCount"/> is not positive.</exception>
    public Partitioner(int partitionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);

        PartitionCount = partitionCount;
        _gates = new object[partitionCount];
        _tails = new Task[partitionCount];
        for (var i = 0; i < partitionCount; i++)
        {
            _gates[i] = new object();
            _tails[i] = Task.CompletedTask;
        }
    }

    /// <summary>
    /// Queues <paramref name="work"/> on the lane selected by <paramref name="key"/>, running it only after
    /// all work already queued on that lane has completed. Submissions for the same key therefore run
    /// sequentially and in submission order; a failed unit of work does not block subsequent work on the lane.
    /// </summary>
    /// <param name="key">The partition key.</param>
    /// <param name="work">The work to run on the key's lane.</param>
    /// <returns>A task that completes when <paramref name="work"/> completes (and faults if it faults).</returns>
    public Task RunSequentialAsync(string key, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(work);

        var lane = GetPartition(key);
        lock (_gates[lane])
        {
            // Enqueue under the lane lock so submission order == lane order. The previous tail is awaited
            // with SuppressThrowing so one failed item never wedges the lane; the caller still observes the
            // outcome of its own work via the returned task.
            var mine = AwaitThenRunAsync(_tails[lane], work);
            _tails[lane] = mine;
            return mine;
        }
    }

    private static async Task AwaitThenRunAsync(Task previous, Func<Task> work)
    {
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await work().ConfigureAwait(false);
    }

    /// <summary>Resolves the lane index a key maps to. Exposed for tests.</summary>
    internal int GetPartition(string key) => (int)(Hash(key) % (uint)PartitionCount);

    // FNV-1a (32-bit) over the UTF-8 bytes of the key. The hash only needs to be deterministic within the
    // process (lanes are in-memory), so a simple, well-distributed hash is sufficient.
    private static uint Hash(string key)
    {
        const uint offsetBasis = 2166136261u;
        const uint prime = 16777619u;

        byte[]? rented = null;
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(key.Length);
        Span<byte> buffer = maxByteCount <= 512
            ? stackalloc byte[512]
            : (rented = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            var written = Encoding.UTF8.GetBytes(key, buffer);
            var hash = offsetBasis;
            foreach (var b in buffer[..written])
            {
                hash ^= b;
                hash *= prime;
            }

            return hash;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
