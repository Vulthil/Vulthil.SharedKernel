using System.Text.Json;
using Vulthil.Messaging.Transport;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.Messaging.Outbox;

/// <summary>
/// Relays <see cref="OutboxDestination.Publish"/> and <see cref="OutboxDestination.Send"/> rows to the broker using
/// the raw transport terminal (<see cref="ITransportPublisher"/> / <see cref="ITransportSendEndpointProvider"/>),
/// bypassing the publish pipeline so the relay is not re-captured by the transactional outbox filter. The stored
/// message id is re-applied, so a redelivered relay is deduplicated by the receiving inbox.
/// </summary>
internal sealed class BrokerOutboxDispatcher(
    ITransportPublisher publisher,
    ITransportSendEndpointProvider sendEndpointProvider,
    IMessageConfigurationProvider messageConfigurationProvider) : IOutboxDispatcher
{
    public bool Handles(OutboxDestination destination) =>
        destination is OutboxDestination.Publish or OutboxDestination.Send;

    public async Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken)
    {
        var messageType = OutboxMessageTypes.Resolve(message.Type);
        var payload = JsonSerializer.Deserialize(message.Content, messageType, messageConfigurationProvider.JsonSerializerOptions)!;
        var metadata = message.Metadata is null
            ? null
            : JsonSerializer.Deserialize<BrokerOutboxMetadata>(message.Metadata, messageConfigurationProvider.JsonSerializerOptions);

        var context = CreateContext(metadata);

        if (message.Destination == OutboxDestination.Send)
        {
            var address = new Uri(metadata?.DestinationAddress
                ?? throw new InvalidOperationException("An outbox send message is missing its destination address."));
            var endpoint = await sendEndpointProvider.GetSendEndpointAsync(address, cancellationToken).ConfigureAwait(false);
            await endpoint.SendAsync(payload, context, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await publisher.PublishAsync(payload, context, cancellationToken).ConfigureAwait(false);
        }
    }

    private static PublishContext CreateContext(BrokerOutboxMetadata? metadata)
    {
        var context = new PublishContext();
        if (metadata is null)
        {
            return context;
        }

        if (!string.IsNullOrEmpty(metadata.MessageId))
        {
            context.SetMessageId(metadata.MessageId);
        }

        if (!string.IsNullOrEmpty(metadata.CorrelationId))
        {
            context.SetCorrelationId(metadata.CorrelationId);
        }

        if (!string.IsNullOrEmpty(metadata.RoutingKey))
        {
            context.SetRoutingKey(metadata.RoutingKey);
        }

        if (metadata.Headers is { Count: > 0 } headers)
        {
            context.AddHeaders(headers);
        }

        return context;
    }
}
