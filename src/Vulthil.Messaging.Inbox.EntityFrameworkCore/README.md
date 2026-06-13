# Vulthil.Messaging.Inbox.EntityFrameworkCore

Shared Entity Framework Core primitives for [Vulthil.Messaging.Inbox](https://www.nuget.org/packages/Vulthil.Messaging.Inbox).
Provider-agnostic within EF Core: it holds the `InboxMessage` marker entity and the `ISaveInboxMessages` context
interface, reused by the provider-specific idempotency stores.

You normally don't reference this directly — install a store instead:

- **`Vulthil.Messaging.Inbox.Relational`** — relational providers (PostgreSQL, SQL Server, MySQL, SQLite), transactional exactly-once.
- **`Vulthil.Messaging.Inbox.Cosmos`** — Azure Cosmos DB, effectively-once.

Both bring this package transitively. Implement `ISaveInboxMessages` on your `DbContext` once; the store and the
provider-specific entity configuration come from the store package.
