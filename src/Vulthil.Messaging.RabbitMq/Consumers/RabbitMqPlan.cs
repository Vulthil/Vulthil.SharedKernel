using Vulthil.Messaging.Queues;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// One queue's dispatch plan for a single concrete message type: the handlers that run on every delivery and,
/// when the type is partitioned, the partition the worker lanes deliveries through. Built once per plan by
/// <see cref="QueueDispatchPlans"/>.
/// </summary>
/// <param name="MessageType">The concrete message type this plan dispatches.</param>
/// <param name="Handlers">The handlers that run on every delivery of <paramref name="MessageType"/>, in plan order.</param>
/// <param name="Partition">The partition to lane deliveries through, or <see langword="null"/> when the type is not partitioned.</param>
internal sealed record RabbitMqPlan(MessageType MessageType, IReadOnlyList<DeliveryHandler> Handlers, RabbitMqPartition? Partition);
