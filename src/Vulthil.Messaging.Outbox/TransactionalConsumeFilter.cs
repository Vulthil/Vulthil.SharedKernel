using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.Messaging.Outbox;

/// <summary>
/// Consume filter that runs the consumer inside a database transaction, so the consumer's database writes and any
/// messages it publishes commit atomically and are captured by the transactional outbox. If an outer filter (such
/// as the inbox) already opened a transaction, this joins it rather than nesting. Opt in per message type with
/// <see cref="TransactionalOutboxExtensions.AddTransactionalConsumer{TMessage}"/>.
/// </summary>
/// <typeparam name="TMessage">The consumed message type.</typeparam>
internal sealed class TransactionalConsumeFilter<TMessage>(IUnitOfWork unitOfWork) : IConsumeFilter<TMessage>
    where TMessage : notnull
{
    public Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next) =>
        unitOfWork.ExecuteInTransactionAsync(
            async _ =>
            {
                await next(context);
                return true;
            },
            context.CancellationToken);
}
