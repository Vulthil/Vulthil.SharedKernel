# Vulthil.Messaging.Inbox.Relational

Relational Entity Framework Core implementation of `IIdempotencyStore` for `Vulthil.Messaging.Inbox`. Gives
**transactional exactly-once** consumer processing on relational databases: the inbox marker and the consumer's
business writes commit in a single transaction, so a redelivered message is never processed twice.

## When to use

- You use `Vulthil.Messaging.Inbox` and persist with EF Core against a **relational** provider (PostgreSQL, SQL Server, MySQL, SQLite)

It relies on relational transactions, so it does **not** support the EF Core Cosmos provider — Cosmos needs a
separate store with a different (effectively-once) mechanism.

## Pattern

- Your `DbContext` implements `ISaveInboxMessages` and maps `InboxMessage`
- The store opens an ambient transaction; the consumer's `SaveChanges` flushes into it without committing
- The marker and the consumer's writes commit together; a consumer exception rolls both back

## Usage

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), ISaveInboxMessages
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyRelationalInbox();
}
```

```csharp
builder.Services.AddRelationalInbox<AppDbContext>();
```

The consumer and the store must resolve the same scoped `DbContext` instance (the default with `AddDbContext`).
Add an EF Core migration for the `InboxMessage` table as you would for any entity. See the
[Inbox Pattern](https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/inbox-pattern.md) article
for details.
