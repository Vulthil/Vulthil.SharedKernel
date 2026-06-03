using System.Reflection;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.RabbitMq.Envelope;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Builds a type-erased partition-key extractor from a registered typed selector, so the worker can read
/// the key from a deserialized message + delivery without knowing the message type generically. The typed
/// closure is built once per message type via <see cref="MethodInfo.MakeGenericMethod"/>.
/// </summary>
internal static class PartitionKeyExtractorFactory
{
    private static readonly MethodInfo BuildTypedMethod = typeof(PartitionKeyExtractorFactory)
        .GetMethod(nameof(BuildTyped), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(PartitionKeyExtractorFactory)}.{nameof(BuildTyped)} not found.");

    public static Func<object, BasicDeliverEventArgs, MessageEnvelope?, string?> Build(Type messageType, Delegate keySelector)
        => (Func<object, BasicDeliverEventArgs, MessageEnvelope?, string?>)BuildTypedMethod
            .MakeGenericMethod(messageType)
            .Invoke(null, [keySelector])!;

    public static Func<object, BasicDeliverEventArgs, MessageEnvelope?, string?> BuildTyped<TMessage>(
        Func<IMessageContext<TMessage>, string?> selector)
        where TMessage : notnull
        => (message, ea, envelope) =>
        {
            // A snapshot context (no live publisher/send provider) is sufficient: key selectors read
            // metadata and the typed message, they do not publish.
            var context = envelope is null
                ? MessageContext.CreateContext((TMessage)message, ea)
                : MessageContext.CreateContext((TMessage)message, ea, envelope, publisher: null, sendEndpointProvider: null, CancellationToken.None);
            return selector(context);
        };
}
