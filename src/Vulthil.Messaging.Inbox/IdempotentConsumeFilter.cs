using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Consume filter that enforces idempotent processing: it resolves the delivery's idempotency key, skips the
/// consumer if the key was already processed, and otherwise runs the consumer inside an
/// <see cref="IIdempotencyStore"/> transaction so the idempotency marker and the consumer's business writes commit
/// together. Registered per message type by <see cref="InboxConfiguratorExtensions.AddIdempotentInbox{TMessage}"/>.
/// </summary>
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
        var key = _keySelector.GetKey(context) ?? context.MessageId;

        if (string.IsNullOrEmpty(key))
        {
            if (_options.RejectMessagesWithoutKey)
            {
                throw new MissingIdempotencyKeyException(typeof(TMessage));
            }

            InboxLog.MissingKeyAllowed(_logger, messageType);
            await next(context);
            return;
        }

        await using var transaction = await _store.BeginAsync(context, context.CancellationToken);

        if (await transaction.HasProcessedAsync(key, context.CancellationToken))
        {
            InboxLog.DuplicateSkipped(_logger, messageType, key);
            return;
        }

        await next(context);
        await transaction.CommitAsync(key, context.CancellationToken);
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
