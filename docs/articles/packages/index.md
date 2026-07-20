# Package usage patterns

Focused usage guidance for each package in `src`. Pick the page that matches the layer you are wiring up; the [Getting Started](../getting-started.md) guide shows how they fit together.

## Domain

- [Vulthil.Results](vulthil-results.md) – `Result<T>` and `Error` for explicit success/failure flows without exceptions.
- [Vulthil.SharedKernel](vulthil-sharedkernel.md) – entity, aggregate root, and domain event primitives.

## Application

- [Vulthil.SharedKernel.Application](vulthil-sharedkernel-application.md) – commands, queries, handlers, FluentValidation integration, and pipeline behaviors.

## Infrastructure

- [Vulthil.SharedKernel.Infrastructure](vulthil-sharedkernel-infrastructure.md) – `BaseDbContext`, generic repository, and outbox composition entry point.
- [Vulthil.SharedKernel.Outbox](vulthil-sharedkernel-outbox.md) – the EF-free transactional outbox engine (relay, dispatchers, `IOutboxStore` seam).
- [Vulthil.SharedKernel.Outbox.EntityFrameworkCore](vulthil-sharedkernel-outbox-entityframeworkcore.md) – the EF Core implementation of the outbox engine.
- [Vulthil.SharedKernel.Infrastructure.Relational](vulthil-sharedkernel-infrastructure-relational.md) – relational outbox store base and `MigrateAsync` for EF Core providers.
- [Vulthil.SharedKernel.Infrastructure.Npgsql](vulthil-sharedkernel-infrastructure-npgsql.md) – `UseNpgsql` provider wiring with PostgreSQL-tuned outbox.
- [Vulthil.SharedKernel.Infrastructure.MySql](vulthil-sharedkernel-infrastructure-mysql.md) – `UseMySql` provider wiring with MySQL-tuned outbox.
- [Vulthil.SharedKernel.Infrastructure.Cosmos](vulthil-sharedkernel-infrastructure-cosmos.md) – `UseCosmosDb` provider wiring with Cosmos-specific outbox.

## API

- [Vulthil.SharedKernel.Api](vulthil-sharedkernel-api.md) – minimal API endpoint conventions and `Result` → HTTP mapping.

## Hosting

- [Vulthil.Extensions.Hosting](vulthil-extensions-hosting.md) – `IRestartableHostedService`, the marker for hosted services that can be paused and resumed cleanly.

## Messaging

- [Vulthil.Messaging.Abstractions](vulthil-messaging-abstractions.md) – transport-agnostic consumer and publisher contracts.
- [Vulthil.Messaging](vulthil-messaging.md) – queue/consumer registration, hosted processing, and the transport-author SDK.
- [Vulthil.Messaging.RabbitMq](vulthil-messaging-rabbitmq.md) – RabbitMQ transport implementation.
- [Vulthil.Messaging.Outbox](vulthil-messaging-outbox.md) – transactional bus-publish outbox bridging the outbox engine to the broker.
- [Vulthil.Messaging.Inbox](vulthil-messaging-inbox.md) – idempotent-receiver consume filter (`IIdempotencyStore` contract).
- [Vulthil.Messaging.Inbox.EntityFrameworkCore](vulthil-messaging-inbox-entityframeworkcore.md) – shared EF Core inbox primitives (`InboxMessage`, `ISaveInboxMessages`).
- [Vulthil.Messaging.Inbox.Relational](vulthil-messaging-inbox-relational.md) – relational idempotency store (transactional exactly-once).
- [Vulthil.Messaging.Inbox.Cosmos](vulthil-messaging-inbox-cosmos.md) – Cosmos idempotency store (effectively-once).

## Testing

- [Vulthil.xUnit](vulthil-xunit.md) – reusable xUnit base classes, auto-mocking, containers, and HTTP mocks.
- [Vulthil.xUnit.Cosmos](vulthil-xunit-cosmos.md) – Cosmos DB emulator fixture for `Vulthil.xUnit`.
- [Vulthil.Messaging.TestHarness](vulthil-messaging-testharness.md) – in-memory messaging test harness.
- [Vulthil.Extensions.Testing](vulthil-extensions-testing.md) – framework-agnostic polling and HTTP response helpers.
