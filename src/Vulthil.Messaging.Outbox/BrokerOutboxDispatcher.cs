using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

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
    private static readonly MethodInfo PublishMethod = typeof(ITransportPublisher)
        .GetMethods()
        .Single(method => method.Name == nameof(ITransportPublisher.PublishAsync));

    private static readonly MethodInfo SendMethod = typeof(ISendEndpoint)
        .GetMethods()
        .Single(method => method.Name == nameof(ISendEndpoint.SendAsync) && method.GetParameters().Length == 3);

    private static readonly ConcurrentDictionary<string, Type> TypeCache = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> PublishByType = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> SendByType = [];

    public bool Handles(OutboxDestination destination) =>
        destination is OutboxDestination.Publish or OutboxDestination.Send;

    public async Task DispatchAsync(OutboxMessageData message, CancellationToken cancellationToken)
    {
        var messageType = ResolveType(message.Type);
        var payload = JsonSerializer.Deserialize(message.Content, messageType, messageConfigurationProvider.JsonSerializerOptions)!;
        var metadata = message.Metadata is null
            ? null
            : JsonSerializer.Deserialize<BrokerOutboxMetadata>(message.Metadata, messageConfigurationProvider.JsonSerializerOptions);

        Func<IPublishContext, ValueTask> configure = context =>
        {
            Apply(metadata, context);
            return ValueTask.CompletedTask;
        };

        if (message.Destination == OutboxDestination.Send)
        {
            var address = new Uri(metadata?.DestinationAddress
                ?? throw new InvalidOperationException("An outbox send message is missing its destination address."));
            var endpoint = await sendEndpointProvider.GetSendEndpointAsync(address, cancellationToken);
            var send = SendByType.GetOrAdd(messageType, static type => SendMethod.MakeGenericMethod(type));
            await (Task)send.Invoke(endpoint, [payload, configure, cancellationToken])!;
        }
        else
        {
            var publish = PublishByType.GetOrAdd(messageType, static type => PublishMethod.MakeGenericMethod(type));
            await (Task)publish.Invoke(publisher, [payload, configure, cancellationToken])!;
        }
    }

    private static void Apply(BrokerOutboxMetadata? metadata, IPublishContext context)
    {
        if (metadata is null)
        {
            return;
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
    }

    private static Type ResolveType(string typeName) => TypeCache.GetOrAdd(typeName, name =>
    {
        var type = Type.GetType(name);
        type ??= AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name))
                .FirstOrDefault(found => found is not null);

        return type!;
    });
}
