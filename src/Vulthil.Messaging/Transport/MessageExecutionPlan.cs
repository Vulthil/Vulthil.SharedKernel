using System.Collections.ObjectModel;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Transport-agnostic execution plan for a single concrete message type: the ordered set of handlers that
/// run on every delivery, plus the partition specification when the type is partitioned for ordered
/// consumption. Built and keyed by <see cref="MessageExecutionRegistry{THandler}"/>.
/// </summary>
/// <typeparam name="THandler">The transport-specific handler type stored in the plan.</typeparam>
/// <param name="MessageType">The concrete message type this plan dispatches.</param>
/// <param name="Urn">The stable wire URN for <paramref name="MessageType"/>.</param>
public sealed record MessageExecutionPlan<THandler>(MessageType MessageType, Uri Urn)
    where THandler : notnull
{
    /// <summary>
    /// The set of handlers that should run when a message of <see cref="MessageType"/> is delivered.
    /// Every handler in this list runs on every delivery; the broker is authoritative for delivery filtering.
    /// </summary>
    public Collection<THandler> Handlers { get; } = [];

    /// <summary>
    /// The partition specification whose lanes serialize same-key deliveries of this message type, or
    /// <see langword="null"/> when the type is not partitioned. Partition key extraction stays transport-side
    /// because it needs the transport's delivery type, so the plan only surfaces the specification.
    /// </summary>
    public PartitionSpec? Partition { get; init; }

    /// <summary>Gets a value indicating whether deliveries of this message type are partitioned for ordered processing.</summary>
    public bool IsPartitioned => Partition is not null;
}
