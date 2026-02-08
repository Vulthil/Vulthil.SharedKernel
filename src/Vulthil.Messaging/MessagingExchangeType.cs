namespace Vulthil.Messaging;

public enum MessagingExchangeType
{
    /// <summary>
    /// Broadcasts messages to all bound queues.
    /// </summary>
    Fanout,

    /// <summary>
    /// Routes messages based on an exact routing key match.
    /// </summary>
    Direct,

    /// <summary>
    /// Routes messages based on wildcard patterns in the routing key.
    /// </summary>
    Topic,

    /// <summary>
    /// Routes messages based on header attributes.
    /// </summary>
    Headers
}
