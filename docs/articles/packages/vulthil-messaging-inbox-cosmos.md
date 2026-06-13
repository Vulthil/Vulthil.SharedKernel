# Vulthil.Messaging.Inbox.Cosmos

Azure Cosmos DB idempotency store for
[Vulthil.Messaging.Inbox](https://www.nuget.org/packages/Vulthil.Messaging.Inbox).

Cosmos has no cross-partition transaction, so — unlike the relational store — it cannot commit the consumer's
writes and the idempotency marker atomically. It provides **effectively-once** processing: best-effort
deduplication of redeliveries over **idempotent-by-design** consumer writes.

## When to use

- You use `Vulthil.Messaging.Inbox` and persist with the EF Core **Cosmos** provider

## Pattern

- Your `DbContext` implements `ISaveInboxMessages` and applies `CosmosInboxMessageEntityConfiguration`
- The marker is a self-contained document keyed + partitioned by `MessageId` in its own container; a duplicate insert conflicts and is treated as already-processed
- The marker is written after the consumer's own commit (no ambient transaction) — keep consumer writes idempotent

## Usage

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), ISaveInboxMessages
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new CosmosInboxMessageEntityConfiguration());
}
```

```csharp
builder.Services.AddCosmosInbox<AppDbContext>();
```

See the [Inbox Pattern](https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/inbox-pattern.md)
article for how the guarantees differ from the relational store.
