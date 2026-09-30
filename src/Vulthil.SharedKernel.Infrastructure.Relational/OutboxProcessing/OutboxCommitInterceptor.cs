using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

/// <summary>
/// Relational transaction interceptor that reports every transaction's start and outcome to
/// <see cref="OutboxRelayWakeup"/>, so the outbox relay wakes as soon as a transaction that saved outbox rows commits
/// instead of waiting for the poll interval, while a commit that saved none (such as the relay's own batch) never
/// wakes it. Attached only when outbox processing is enabled on a relational provider.
/// </summary>
internal sealed class OutboxCommitInterceptor(OutboxRelayWakeup relayWakeup) : DbTransactionInterceptor, IOutboxInterceptor
{
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        ReportStarted(eventData);
        return base.TransactionStarted(connection, eventData, result);
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        ReportStarted(eventData);
        return base.TransactionStartedAsync(connection, eventData, result, cancellationToken);
    }

    public override DbTransaction TransactionUsed(DbConnection connection, TransactionEventData eventData, DbTransaction result)
    {
        ReportStarted(eventData);
        return base.TransactionUsed(connection, eventData, result);
    }

    public override ValueTask<DbTransaction> TransactionUsedAsync(DbConnection connection, TransactionEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        ReportStarted(eventData);
        return base.TransactionUsedAsync(connection, eventData, result, cancellationToken);
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        ReportCommitted(eventData);
        base.TransactionCommitted(transaction, eventData);
    }

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ReportCommitted(eventData);
        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        ReportEndedWithoutCommit(eventData);
        base.TransactionRolledBack(transaction, eventData);
    }

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ReportEndedWithoutCommit(eventData);
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
    {
        ReportEndedWithoutCommit(eventData);
        base.TransactionFailed(transaction, eventData);
    }

    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ReportEndedWithoutCommit(eventData);
        return base.TransactionFailedAsync(transaction, eventData, cancellationToken);
    }

    private void ReportStarted(TransactionEventData eventData)
    {
        if (eventData.Context is { } context)
        {
            relayWakeup.TransactionStarted(context);
        }
    }

    private void ReportCommitted(TransactionEventData eventData)
    {
        if (eventData.Context is { } context)
        {
            relayWakeup.TransactionCommitted(context);
        }
    }

    private void ReportEndedWithoutCommit(TransactionEventData eventData)
    {
        if (eventData.Context is { } context)
        {
            relayWakeup.TransactionRolledBack(context);
        }
    }
}
