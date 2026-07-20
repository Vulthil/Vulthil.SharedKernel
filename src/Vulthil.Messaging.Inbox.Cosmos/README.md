# Vulthil.Messaging.Inbox.Cosmos

[![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.Inbox.Cosmos)](https://www.nuget.org/packages/Vulthil.Messaging.Inbox.Cosmos)

Azure Cosmos DB idempotency store for [Vulthil.Messaging.Inbox](https://www.nuget.org/packages/Vulthil.Messaging.Inbox).

Cosmos has no cross-partition transaction, so — unlike the relational store — it cannot commit the consumer's
writes and the idempotency marker atomically. This store therefore provides **effectively-once** processing:
best-effort deduplication of redeliveries layered over **idempotent-by-design** consumer writes. Use deterministic
document ids / upserts in your consumers so the rare interleaving the marker can't guard is harmless.

## Usage

Expose the inbox set on your Cosmos `DbContext` and apply the mapping:

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), ISaveInboxMessages
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyCosmosInbox();
}
```

```csharp
builder.Services.AddCosmosInbox<AppDbContext>();

builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq(/* ... */);
    messaging.ConfigureQueue("orders", q => q.AddConsumer<OrderPlacedConsumer>());
    messaging.AddIdempotentInbox<OrderPlaced>();
});
```

The marker is a self-contained document keyed and partitioned by `MessageId` (its own container), so duplicate
inserts conflict and are treated as already-processed. See the Inbox Pattern article on the
[documentation site](https://vulthil.github.io/Vulthil.SharedKernel/) for the guarantees and how this differs
from the relational store.
