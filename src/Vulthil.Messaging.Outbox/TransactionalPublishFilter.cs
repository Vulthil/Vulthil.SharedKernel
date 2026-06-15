using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Transport;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.Messaging.Outbox;

/// <summary>
/// Publish/send filter that captures an outgoing message into the shared outbox table when a database transaction
/// is active, so it is persisted atomically with the business changes and relayed to the broker only after the
/// transaction commits. When no transaction is active the publish proceeds directly.
/// </summary>
internal sealed class TransactionalPublishFilter(
    IOutboxStore outboxStore,
    IMessageConfigurationProvider messageConfigurationProvider,
    IOptions<OutboxProcessingOptions> outboxProcessingOptions,
    TimeProvider timeProvider) : IPublishFilter
{
    public async Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next)
    {
        if (!outboxStore.IsInTransaction)
        {
            await next(context);
            return;
        }

        outboxStore.AddOutboxMessage(CreateRow(context));

        await outboxStore.SaveChangesAsync(context.CancellationToken);
    }

    private OutboxMessage CreateRow(PublishFilterContext context)
    {
        var activity = outboxProcessingOptions.Value.EnableTracing ? Activity.Current : null;

        var metadata = new BrokerOutboxMetadata
        {
            MessageId = context.Context.MessageId,
            CorrelationId = context.Context.CorrelationId,
            RoutingKey = context.Context.RoutingKey,
            DestinationAddress = context.DestinationAddress?.ToString(),
            Headers = context.Context.Headers.Count > 0
                ? new Dictionary<string, object?>(context.Context.Headers)
                : null,
        };

        return new OutboxMessage
        {
            Type = context.MessageType.FullName!,
            Content = JsonSerializer.Serialize(context.Message, context.MessageType, messageConfigurationProvider.JsonSerializerOptions),
            OccurredOnUtc = timeProvider.GetUtcNow(),
            Destination = context.Kind == PublishKind.Send ? OutboxDestination.Send : OutboxDestination.Publish,
            TraceParent = activity?.Id,
            TraceState = activity?.TraceStateString,
            Metadata = JsonSerializer.Serialize(metadata, messageConfigurationProvider.JsonSerializerOptions),
        };
    }
}
