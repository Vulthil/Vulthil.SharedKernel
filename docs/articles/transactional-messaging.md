# Transactional Messaging

The [outbox](outbox-pattern.md) protects the producer side and the [inbox](inbox-pattern.md) protects the consumer
side. Together they give end-to-end effectively-once delivery: a message captured in the outbox carries a stable
message id from capture through relay, and the inbox deduplicates on that id. This page is the complete wiring for
both, in one place; the pattern articles explain the guarantees.

## The four seams

Enabling both touches four seams. Each is a separate module with its own entry point, and that is deliberate:

| Concern | Entry point | Call | Package |
|---|---|---|---|
| Database + outbox engine | `IHostApplicationBuilder` | `AddDbContext<AppDbContext>(db => db.UseNpgsql("Default").EnableOutboxProcessing())` | `Vulthil.SharedKernel.Infrastructure` + `.Npgsql` |
| Inbox store | `IServiceCollection` | `AddRelationalInbox<AppDbContext>()` | `Vulthil.Messaging.Inbox.Relational` |
| Message pipeline | `IMessagingConfigurator` | `AddIdempotentInbox<T>()`, `AddTransactionalOutbox()`, `AddTransactionalConsumer<T>()` | `Vulthil.Messaging.Inbox`, `Vulthil.Messaging.Outbox` |
| Schema | `ModelBuilder` | `ApplyNpgsqlOutbox()`, `ApplyRelationalInbox()` | the provider packages |

- The database registration is a **host-level** decision, so it lives on the host builder.
- The inbox store is a **module-level** registration on `IServiceCollection`, so the project that owns the
  `DbContext` can register it without owning the host's composition root. It deliberately does not depend on the
  SharedKernel database configurator, so it also serves a `DbContext` registered with plain `AddDbContext`.
- Idempotency and transactional consumption are **per-message-type** opt-ins, so they belong on the messaging
  configurator, next to the consumers they guard.
- The schema stays **in code**, in `OnModelCreating`, so the design-time model (`dotnet ef migrations add`, a
  design-time factory) and the runtime model are identical. A mapping applied from the host's service registrations
  would be invisible to a design-time factory, and EF Core 9+ refuses to migrate a model with pending changes.

## 1. The `DbContext`

Derive from `BaseDbContext` (outbox set, `IUnitOfWork`), implement `ISaveInboxMessages` (inbox set), and apply both
mappings:

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : BaseDbContext(options), ISaveInboxMessages
{
    protected override Assembly? ConfigurationAssembly => typeof(AppDbContext).Assembly;

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyNpgsqlOutbox();
        modelBuilder.ApplyRelationalInbox();
    }
}
```

Both entities are valid by convention alone — `OutboxMessage.Id` is a conventional key and `InboxMessage.MessageId`
is annotated as a bounded key — so a missing `Apply*` call does not break the model. What the calls add is the
provider-optimized layout: for PostgreSQL a `jsonb` content column and a filtered index over the pending rows, for
MySQL a `longtext` column and a composite pending index, for Cosmos the dedicated inbox container and partition key.
Without them the relay and the inbox still work, only on the conventional table.

## 2. The host

```csharp
var builder = WebApplication.CreateBuilder(args);

// Database with the outbox engine (Vulthil.SharedKernel.Infrastructure + .Npgsql).
builder.AddDbContext<AppDbContext>(db => db
    .UseNpgsql("Default")
    .EnableOutboxProcessing(o => o.Retention.Enabled = true));

// Inbox store on the same context (Vulthil.Messaging.Inbox.Relational).
builder.Services.AddRelationalInbox<AppDbContext>(o => o.Retention.Enabled = true);

// Messaging (Vulthil.Messaging.RabbitMq, .Inbox, .Outbox).
builder.AddMessaging(messaging =>
{
    messaging.ConfigureQueue("orders", queue => queue.AddConsumer<OrderPlacedConsumer>());

    messaging.AddIdempotentInbox<OrderPlaced>(context => context.Message.OrderId.ToString());
    messaging.AddTransactionalConsumer<OrderPlaced>();
    messaging.AddTransactionalOutbox();

    messaging.UseRabbitMq("RabbitMq");
});

var app = builder.Build();
await app.MigrateAsync<AppDbContext>();
```

Two orderings matter, both inside `AddMessaging`:

- Register `AddIdempotentInbox<T>()` **before** `AddTransactionalConsumer<T>()`, so the inbox owns the transaction
  and the transactional consumer joins it (see [filter registration order](inbox-pattern.md#filter-registration-order)).
- Register any publish filter that must observe every outgoing message **before** `AddTransactionalOutbox()`,
  because capture short-circuits the publish pipeline (see [the bus outbox](outbox-pattern.md#transactional-bus-publish-outbox)).

Everything else is order-independent: `UseNpgsql`, `EnableOutboxProcessing`, and `UseOutboxStore` may be chained in
any order, and `AddRelationalInbox` may run before or after `AddDbContext`.

## 3. The schema

Add one migration for both tables and apply it on startup, as above:

```bash
dotnet ef migrations add TransactionalMessaging
```

Cosmos has no migrations — call `EnsureCreatedAsync<AppDbContext>()` instead.

## Provider variants

| Provider | Database registration | Outbox mapping | Inbox store | Inbox mapping |
|---|---|---|---|---|
| PostgreSQL | `UseNpgsql` | `ApplyNpgsqlOutbox()` | `AddRelationalInbox<T>()` | `ApplyRelationalInbox()` |
| MySQL | `UseMySql` | `ApplyMySqlOutbox()` | `AddRelationalInbox<T>()` | `ApplyRelationalInbox()` |
| Cosmos DB | `UseCosmosDb` | `ApplyCosmosOutbox()` | `AddCosmosInbox<T>()` | `ApplyCosmosInbox()` |

On Cosmos the inbox is effectively-once and the outbox relay runs without a transaction; the pattern articles list
the guarantee per store.

## What happens at runtime

1. A transactional command (`ITransactionalCommand<T>` with `AddTransactionalPipelineBehavior()`), or a consumer
   under the inbox or transactional filter, opens a transaction.
2. Domain events raised during `SaveChanges` and messages published through the bus are captured as `OutboxMessage`
   rows in that transaction.
3. On commit the relay wakes and publishes the rows to the broker, carrying the stable message id.
4. On the consumer side the inbox filter checks the id, runs the consumer inside a transaction, and commits the
   marker together with the consumer's writes. A redelivery finds the marker and is skipped.

The `samples/WebApi` project wires exactly this against PostgreSQL and RabbitMQ.

## Custom stores

- **Outbox**: register your own `IOutboxStore` with `UseOutboxStore<TStore>()`. The provider extensions propose
  their store through `UseDefaultOutboxStore<TStore>()`, which only applies when nothing was selected, so your
  selection wins in any order. A custom provider package should use the same call.
- **Inbox**: register your `IIdempotencyStore` and call `AddInboxCore()`; see
  [custom stores](inbox-pattern.md#custom-stores).
