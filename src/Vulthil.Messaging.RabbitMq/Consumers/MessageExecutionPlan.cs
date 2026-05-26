using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal sealed record MessageExecutionPlan(MessageType MessageType)
{
    /// <summary>
    /// The set of handlers that should run when a message of <see cref="MessageType"/> is delivered.
    /// The broker is authoritative for delivery (queue-binding filter); every handler in this list runs on every delivery.
    /// </summary>
    public List<MessageHandler> Handlers { get; } = [];
}
