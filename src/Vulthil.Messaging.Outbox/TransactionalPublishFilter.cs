using System.Diagnostics;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Transport;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.Messaging.Outbox;

/// <summary>
/// Publish/send filter that captures an outgoing message into the shared outbox table when a database transaction
/// is active, so it is persisted atomically with the business changes and relayed to the broker only after the
/// transaction commits. When no transaction is active the publish proceeds directly. Ambient
/// <see cref="System.Transactions.TransactionScope"/> transactions are not supported (see <see cref="PublishAsync"/>).
/// </summary>
internal sealed class TransactionalPublishFilter(
    IOutboxStore outboxStore,
    IMessageConfigurationProvider messageConfigurationProvider,
    IOptions<OutboxProcessingOptions> outboxProcessingOptions,
    TimeProvider timeProvider) : IPublishFilter
{
    /// <exception cref="NotSupportedException">
    /// An ambient <see cref="System.Transactions.TransactionScope"/> is active but the outbox store reports no
    /// Entity Framework Core transaction, so capture cannot enlist and a direct publish would escape the scope.
    /// </exception>
    public async Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next)
    {
        if (!outboxStore.IsInTransaction)
        {
            if (Transaction.Current is not null)
            {
                throw new NotSupportedException(
                    "TransactionScope-based ambient transactions are not supported by the transactional outbox: the " +
                    "outbox store reports no active Entity Framework Core transaction, so this message cannot be " +
                    "captured and would otherwise publish directly while the scope is uncommitted. Use " +
                    "IUnitOfWork.ExecuteInTransactionAsync, a transactional command, or a transactional consumer to " +
                    "establish the transaction instead.");
            }

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
