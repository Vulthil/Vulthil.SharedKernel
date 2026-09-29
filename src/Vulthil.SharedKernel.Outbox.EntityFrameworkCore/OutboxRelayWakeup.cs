using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

/// <summary>
/// Wakes the outbox relay through <see cref="IOutboxSignal"/> once each time outbox rows inserted through a
/// <see cref="DbContext"/> become durable: right after a save that ran outside a transaction, or when the transaction
/// the save ran in commits. A save that inserts no outbox rows never wakes the relay — so the relay's own bookkeeping,
/// which only updates rows, cannot wake it and cut its failure back-off short — and a transaction that rolls back or
/// fails forgets the rows it saved.
/// </summary>
/// <remarks>
/// <see cref="DomainEventsToOutboxMessageSaveChangesInterceptor"/> reports every save. A transaction-capable provider
/// reports its transactions through <see cref="TransactionStarted"/>, <see cref="TransactionCommitted"/> and
/// <see cref="TransactionRolledBack"/>; the relational providers do so through the transaction interceptor that
/// <c>AddRelationalOutboxCommitTrigger</c> registers. Without those reports, rows saved inside a transaction are
/// relayed by the next poll. One instance per container (a singleton) serves every context and every reporter; the
/// state it keeps per context lives only as long as that context instance.
/// </remarks>
public sealed class OutboxRelayWakeup
{
    private readonly IOutboxSignal _signal;
    private readonly ConditionalWeakTable<DbContext, UnitState> _units = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxRelayWakeup"/> class.
    /// </summary>
    /// <param name="signal">The signal that wakes the outbox relay.</param>
    public OutboxRelayWakeup(IOutboxSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        _signal = signal;
    }

    /// <summary>
    /// Reports that a transaction started on <paramref name="context"/>. Rows remembered from an earlier transaction
    /// that ended without a report (for example one disposed without a commit) are forgotten, so they never wake the
    /// relay.
    /// </summary>
    /// <param name="context">The context the transaction started on.</param>
    public void TransactionStarted(DbContext context) => ForgetRowsAwaitingCommit(context);

    /// <summary>
    /// Reports that the transaction on <paramref name="context"/> committed, waking the relay once when the
    /// transaction saved outbox rows.
    /// </summary>
    /// <param name="context">The context whose transaction committed.</param>
    public void TransactionCommitted(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_units.TryGetValue(context, out var unit) && unit.RowsAwaitingCommit)
        {
            unit.RowsAwaitingCommit = false;
            _signal.Notify();
        }
    }

    /// <summary>
    /// Reports that the transaction on <paramref name="context"/> rolled back or failed. The outbox rows it saved were
    /// never committed, so they are forgotten.
    /// </summary>
    /// <param name="context">The context whose transaction ended without a commit.</param>
    public void TransactionRolledBack(DbContext context) => ForgetRowsAwaitingCommit(context);

    /// <summary>
    /// Records whether the save starting on <paramref name="context"/> inserts outbox rows. The capture interceptor
    /// calls this after adding the captured domain events, so they count.
    /// </summary>
    /// <param name="context">The context whose save is starting.</param>
    internal void SavingChanges(DbContext context)
    {
        var insertsOutboxRows = context.ChangeTracker.Entries<OutboxMessage>().Any(entry => entry.State == EntityState.Added);

        if (insertsOutboxRows)
        {
            GetOrAddUnit(context).RowsInSave = true;
        }
        else if (_units.TryGetValue(context, out var unit))
        {
            unit.RowsInSave = false;
        }
    }

    /// <summary>
    /// Settles the save that completed on <paramref name="context"/>. Outside a transaction the rows it inserted are
    /// durable now and wake the relay; inside one they wait for the commit.
    /// </summary>
    /// <param name="context">The context whose save completed.</param>
    internal void SavedChanges(DbContext context)
    {
        if (!_units.TryGetValue(context, out var unit) || !unit.RowsInSave)
        {
            return;
        }

        unit.RowsInSave = false;

        if (context.Database.CurrentTransaction is null)
        {
            _signal.Notify();
        }
        else
        {
            unit.RowsAwaitingCommit = true;
        }
    }

    private void ForgetRowsAwaitingCommit(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_units.TryGetValue(context, out var unit))
        {
            unit.RowsAwaitingCommit = false;
        }
    }

    private UnitState GetOrAddUnit(DbContext context) => _units.GetValue(context, static _ => new UnitState());

    private sealed class UnitState
    {
        public bool RowsInSave { get; set; }

        public bool RowsAwaitingCommit { get; set; }
    }
}
