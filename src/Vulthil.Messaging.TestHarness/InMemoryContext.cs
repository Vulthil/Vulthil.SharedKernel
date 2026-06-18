using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// Helpers for the in-memory receive path: deserializes an envelope payload and builds a live
/// <see cref="MessageContext{TMessage}"/> bound to the harness's in-memory publisher and send-endpoint
/// provider, so a consumer's nested publishes and sends flow back through the harness.
/// </summary>
internal static class InMemoryContext
{
    public static TMessage Deserialize<TMessage>(IServiceProvider scope, MessageEnvelope envelope)
        where TMessage : notnull
    {
        var options = scope.GetRequiredService<IMessageConfigurationProvider>().JsonSerializerOptions;
        return envelope.Message.Deserialize<TMessage>(options)
            ?? throw new InvalidOperationException($"The in-memory transport could not deserialize a '{envelope.MessageType}' payload.");
    }

    public static MessageContext<TMessage> Create<TMessage>(IServiceProvider scope, TMessage message, MessageEnvelope envelope, CancellationToken cancellationToken, int retryCount = 0)
        where TMessage : notnull
        => MessageContext.CreateFromEnvelope(
            message,
            envelope,
            routingKey: string.Empty,
            redelivered: retryCount > 0,
            retryCount: retryCount,
            replyToFallback: null,
            scope.GetRequiredService<IPublisher>(),
            scope.GetRequiredService<ISendEndpointProvider>(),
            cancellationToken);
}
