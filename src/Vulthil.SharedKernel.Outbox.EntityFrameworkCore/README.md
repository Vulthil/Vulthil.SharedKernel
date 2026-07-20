# Vulthil.SharedKernel.Outbox.EntityFrameworkCore

[![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Outbox.EntityFrameworkCore)](https://www.nuget.org/packages/Vulthil.SharedKernel.Outbox.EntityFrameworkCore)

The Entity Framework Core implementation of the
[`Vulthil.SharedKernel.Outbox`](https://www.nuget.org/packages/Vulthil.SharedKernel.Outbox)
engine. It keeps all EF Core coupling out of the engine package:

- `ISaveOutboxMessages` — the `DbSet<OutboxMessage>` marker the application's `DbContext` implements.
- `EntityFrameworkOutboxStore<TContext>` — the `IOutboxStore` implementation: the transactional relay batch unit
  (execution strategy, transaction, fetch, dispatch, mark, commit) plus the capture surface (`AddOutboxMessage` /
  `SaveChangesAsync` / `IsInTransaction`). Provider packages override fetch/mark/transaction for row-level locking
  and best-effort (Cosmos) behaviour.
- `DomainEventsToOutboxMessageSaveChangesInterceptor` / `IOutboxInterceptor` — capture of aggregate domain events.
- `ApplyOutbox()` — a `ModelBuilder` extension applying the provider-agnostic `OutboxMessage` mapping. Provider packages offer optimized alternatives (`ApplyNpgsqlOutbox()`, `ApplyMySqlOutbox()`, `ApplyCosmosOutbox()`).

Most applications consume this transitively via `Vulthil.SharedKernel.Infrastructure` (`EnableOutboxProcessing`) and
a provider package (`UseNpgsql`, `UseMySql`, `UseCosmosDb`). See the Outbox Pattern article on the
[documentation site](https://vulthil.github.io/Vulthil.SharedKernel/).
