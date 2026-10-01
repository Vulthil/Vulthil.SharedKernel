# Vulthil.SharedKernel.Outbox.EntityFrameworkCore

The Entity Framework Core implementation of the [`Vulthil.SharedKernel.Outbox`](vulthil-sharedkernel-outbox.md)
engine. It isolates all EF Core coupling so the engine package stays persistence-agnostic.

## When to use

- You normally get it transitively via `Vulthil.SharedKernel.Infrastructure` (`EnableOutboxProcessing`) plus a
  provider package (`UseNpgsql`, `UseMySql`, `UseCosmosDb`).
- Reference it directly to implement a custom EF `IOutboxStore` (derive from `EntityFrameworkOutboxStore<TContext>`),
  or to have a `DbContext` implement `ISaveOutboxMessages` without the full infrastructure package.

## What's here

- `ISaveOutboxMessages` — the `DbSet<OutboxMessage>` marker the application's `DbContext` implements.
- `EntityFrameworkOutboxStore<TContext>` — the `IOutboxStore` implementation: the relay's transactional boundary
  (execution strategy, transaction, claim, record, commit; the engine dispatches) plus the capture surface. Provider
  packages override fetch/mark/transaction for row-level locking and Cosmos best-effort behaviour.
- `DomainEventsToOutboxMessageSaveChangesInterceptor` / `IOutboxInterceptor` — domain-event capture.
- `OutboxRelayWakeup` — wakes the relay once each time outbox rows a context inserted become durable: right after a
  save outside a transaction, or when the transaction that saved them commits (reported by a transaction-capable
  provider, e.g. the relational `AddRelationalOutboxCommitTrigger()`). Saves that insert no outbox rows never wake it.
- `ApplyOutbox()` — a `ModelBuilder` extension applying the provider-agnostic `OutboxMessage` mapping. Provider packages offer optimized alternatives (`ApplyNpgsqlOutbox()`, `ApplyMySqlOutbox()`, `ApplyCosmosOutbox()`).

See the [Outbox Pattern](../outbox-pattern.md)
article for the design.
