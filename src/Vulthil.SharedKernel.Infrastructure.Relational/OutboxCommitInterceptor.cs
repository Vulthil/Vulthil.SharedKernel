using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

/// <summary>
/// Relational transaction-commit interceptor that wakes the outbox relay (<see cref="IOutboxSignal"/>) as soon as a
/// transaction commits, so freshly-committed outbox messages are dispatched without waiting for the poll interval.
/// Attached only when outbox processing is enabled on a relational provider.
/// </summary>
internal sealed class OutboxCommitInterceptor(IOutboxSignal signal) : DbTransactionInterceptor, IOutboxInterceptor
{
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        signal.Notify();
        base.TransactionCommitted(transaction, eventData);
    }

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        signal.Notify();
        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }
}
