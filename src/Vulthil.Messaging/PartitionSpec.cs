namespace Vulthil.Messaging;

/// <summary>
/// Records how a message type is partitioned for ordered consumption: the <see cref="Partitioner"/>
/// whose lanes serialize same-key processing, and the typed key selector (held as a <see cref="Delegate"/>
/// because the message type is only known generically at registration). The transport resolves the
/// concrete selector via the registered message type.
/// </summary>
public sealed record PartitionSpec(Partitioner Partitioner, Delegate KeySelector);
