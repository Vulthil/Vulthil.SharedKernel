# Package usage patterns

Focused usage guidance for each package in `src`. Pick the page that matches the layer you are wiring up; the [Getting Started](../getting-started.md) guide shows how they fit together.

## Domain

- [Vulthil.Results](vulthil-results.md) – `Result<T>` and `Error` for explicit success/failure flows without exceptions.
- [Vulthil.SharedKernel](vulthil-sharedkernel.md) – entity, aggregate root, and domain event primitives.

## Application

- [Vulthil.SharedKernel.Application](vulthil-sharedkernel-application.md) – commands, queries, handlers, FluentValidation integration, and pipeline behaviors.

## Infrastructure

- [Vulthil.SharedKernel.Infrastructure](vulthil-sharedkernel-infrastructure.md) – `BaseDbContext`, generic repository, and outbox composition entry point.
- [Vulthil.SharedKernel.Infrastructure.Relational](vulthil-sharedkernel-infrastructure-relational.md) – relational outbox strategy and `MigrateAsync` for EF Core providers.
- [Vulthil.SharedKernel.Infrastructure.Npgsql](vulthil-sharedkernel-infrastructure-npgsql.md) – `UseNpgsql` provider wiring with PostgreSQL-tuned outbox.
- [Vulthil.SharedKernel.Infrastructure.MySql](vulthil-sharedkernel-infrastructure-mysql.md) – `UseMySql` provider wiring with MySQL-tuned outbox.
- [Vulthil.SharedKernel.Infrastructure.Cosmos](vulthil-sharedkernel-infrastructure-cosmos.md) – `UseCosmosDb` provider wiring with Cosmos-specific outbox.

## API

- [Vulthil.SharedKernel.Api](vulthil-sharedkernel-api.md) – minimal API endpoint conventions and `Result` → HTTP mapping.

## Messaging

- [Vulthil.Messaging.Abstractions](vulthil-messaging-abstractions.md) – transport-agnostic consumer and publisher contracts.
- [Vulthil.Messaging](vulthil-messaging.md) – queue/consumer registration and hosted processing.
- [Vulthil.Messaging.RabbitMq](vulthil-messaging-rabbitmq.md) – RabbitMQ transport implementation.

## Testing

- [Vulthil.xUnit](vulthil-xunit.md) – reusable xUnit base classes and auto-mocking.
- [Vulthil.Messaging.TestHarness](vulthil-messaging-testharness.md) – in-memory messaging test harness.
- [Vulthil.Extensions.Testing](vulthil-extensions-testing.md) – shared assertion and setup helpers.
