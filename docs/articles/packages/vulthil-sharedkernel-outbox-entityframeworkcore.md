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
- `EntityFrameworkOutboxStore<TContext>` — the `IOutboxStore` implementation: the transactional relay batch unit
  (execution strategy, transaction, fetch, dispatch, mark, commit) plus the capture surface. Provider packages
  override fetch/mark/transaction for row-level locking and Cosmos best-effort behaviour.
- `DomainEventsToOutboxMessageSaveChangesInterceptor` / `IOutboxInterceptor` — domain-event capture.
- `ApplyOutbox()` — a `ModelBuilder` extension applying the provider-agnostic `OutboxMessage` mapping. Provider packages offer optimized alternatives (`ApplyNpgsqlOutbox()`, `ApplyMySqlOutbox()`, `ApplyCosmosOutbox()`).

See the [Outbox Pattern](../outbox-pattern.md)
article for the design.
