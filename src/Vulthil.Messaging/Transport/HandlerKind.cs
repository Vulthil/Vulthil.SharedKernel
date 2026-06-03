using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Classifies a dispatch handler by its consumer contract.
/// </summary>
public enum HandlerKind
{
    /// <summary>A one-way <see cref="IConsumer{TMessage}"/>.</summary>
    Consumer,
    /// <summary>A request/reply <see cref="IRequestConsumer{TRequest, TResponse}"/>.</summary>
    RequestConsumer,
}
