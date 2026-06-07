# Vulthil.Messaging.Inbox.Relational

Relational Entity Framework Core idempotency store for
[Vulthil.Messaging.Inbox](https://www.nuget.org/packages/Vulthil.Messaging.Inbox). Gives **transactional
exactly-once** consumer processing on relational databases: the inbox marker and the consumer's business writes
commit in a single transaction, so a redelivered message is never processed twice.

Works with any relational EF Core provider (PostgreSQL, SQL Server, MySQL, SQLite). It relies on relational
transactions, so it does **not** support the EF Core Cosmos provider — Cosmos needs a separate store with a
different (effectively-once) mechanism.

## Usage

Expose the inbox set on your `DbContext` and map the entity:

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), ISaveInboxMessages
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfiguration(new InboxMessageEntityConfiguration());
}
```

Register the store and opt your messages into the inbox:

```csharp
builder.Services.AddRelationalInbox<AppDbContext>();

builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq(/* ... */);
    messaging.ConfigureQueue("orders", q => q.AddConsumer<OrderPlacedConsumer>());
    messaging.AddIdempotentInbox<OrderPlaced>();
});
```

The consumer keeps calling `SaveChanges` as usual — the store opens an ambient transaction, so those writes flush
without committing and are committed together with the idempotency marker. Add an EF Core migration for the
`InboxMessage` table as you would for any entity.
