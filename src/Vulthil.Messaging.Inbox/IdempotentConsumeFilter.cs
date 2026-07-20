using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Consume filter that enforces idempotent processing: it resolves the delivery's idempotency key and hands the
/// consumer invocation to an <see cref="IIdempotencyStore"/>, which skips it if the key was already processed and
/// otherwise runs it and records the idempotency marker — atomically with the consumer's business writes on a
/// relational provider. Registered per message type by <see cref="InboxConfiguratorExtensions.AddIdempotentInbox{TMessage}"/>.
/// </summary>
/// <remarks>
/// The guard only deduplicates. Bounding a consumer that keeps failing — retry count, fault emission, and
/// dead-lettering — is delegated to the transport (on RabbitMQ: retry per the consumer's resolved retry policy,
/// then a fault is published and the delivery is dead-lettered when a dead-letter queue is configured), so this
/// filter deliberately ignores <see cref="IMessageContext.RetryCount"/>.
/// </remarks>
/// <typeparam name="TMessage">The consumed message type.</typeparam>
internal sealed class IdempotentConsumeFilter<TMessage>(
    IIdempotencyStore store,
    IInboxKeySelector<TMessage> keySelector,
    IOptions<InboxOptions> options,
    ILogger<IdempotentConsumeFilter<TMessage>> logger) : IConsumeFilter<TMessage>
    where TMessage : notnull
{
    private readonly IIdempotencyStore _store = store;
    private readonly IInboxKeySelector<TMessage> _keySelector = keySelector;
    private readonly InboxOptions _options = options.Value;
    private readonly ILogger<IdempotentConsumeFilter<TMessage>> _logger = logger;

    public async Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next)
    {
        var messageType = typeof(TMessage).FullName ?? typeof(TMessage).Name;
        var key = _keySelector.GetKey(context);
        key = string.IsNullOrEmpty(key) ? context.MessageId : key;

        if (string.IsNullOrEmpty(key))
        {
            if (_options.RejectMessagesWithoutKey)
            {
                throw new MissingIdempotencyKeyException(typeof(TMessage));
            }

            InboxLog.MissingKeyAllowed(_logger, messageType);
            InboxTelemetry.MissingKey.Add(1);
            await next(context);
            return;
        }

        var processed = await _store.ProcessAsync(key, context, _ => next(context), context.CancellationToken);

        if (processed)
        {
            InboxTelemetry.Processed.Add(1);
        }
        else
        {
            InboxLog.DuplicateSkipped(_logger, messageType, key);
            InboxTelemetry.DuplicateSkipped.Add(1);
        }
    }
}

internal static partial class InboxLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Debug,
        Message = "Skipping already-processed {MessageType} (idempotencyKey={IdempotencyKey})")]
    public static partial void DuplicateSkipped(ILogger logger, string messageType, string idempotencyKey);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Debug,
        Message = "Processing {MessageType} without deduplication: no idempotency key was resolved")]
    public static partial void MissingKeyAllowed(ILogger logger, string messageType);
}
