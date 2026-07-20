# Vulthil.Messaging.Inbox.EntityFrameworkCore

Shared Entity Framework Core primitives for
[Vulthil.Messaging.Inbox](https://www.nuget.org/packages/Vulthil.Messaging.Inbox) — provider-agnostic within EF
Core. It holds the `InboxMessage` marker entity and the `ISaveInboxMessages` context interface, reused by the
provider-specific stores.

## When to use

You normally don't reference this directly. Install a store, which brings this transitively:

- **`Vulthil.Messaging.Inbox.Relational`** — PostgreSQL, SQL Server, MySQL, SQLite (transactional exactly-once)
- **`Vulthil.Messaging.Inbox.Cosmos`** — Azure Cosmos DB (effectively-once)

## Pattern

Implement `ISaveInboxMessages` on your `DbContext` **once**; the provider-specific entity configuration and store
come from the store package. This mirrors how `Microsoft.EntityFrameworkCore` is the base for
`Microsoft.EntityFrameworkCore.Relational` / `.Cosmos`.

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), ISaveInboxMessages
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
}
```

See the [Inbox Pattern](../inbox-pattern.md)
article for the full design.
