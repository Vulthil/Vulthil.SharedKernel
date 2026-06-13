<div align="center">

# Vulthil.SharedKernel

<img src="Vulthil.SharedKernel.png" alt="Vulthil.SharedKernel" width="200" />

**Opinionated .NET building blocks for domain-driven design, CQRS, messaging, and testing.**

[![Build and Test](https://github.com/Vulthil/Vulthil.SharedKernel/actions/workflows/ci.yml/badge.svg)](https://github.com/Vulthil/Vulthil.SharedKernel/actions/workflows/ci.yml)
[![Release](https://github.com/Vulthil/Vulthil.SharedKernel/actions/workflows/release.yml/badge.svg)](https://github.com/Vulthil/Vulthil.SharedKernel/actions/workflows/release.yml)
[![Coverage](https://gist.githubusercontent.com/Vulthil/2852f1400e27493b185559c76b38e9b7/raw/badge_combined.svg)](https://github.com/Vulthil/Vulthil.SharedKernel/actions/workflows/ci.yml)

[Documentation](https://vulthil.github.io/Vulthil.SharedKernel/) ·
[Articles](docs/articles) ·
[Report a Bug](https://github.com/Vulthil/Vulthil.SharedKernel/issues/new?template=bug_report.yml) ·
[Request a Feature](https://github.com/Vulthil/Vulthil.SharedKernel/issues/new?template=feature_request.yml)

</div>

---

## Overview

Vulthil.SharedKernel is a modular collection of NuGet packages that provide reusable
foundations for building maintainable .NET applications. Each package is focused on a
single concern — from result primitives and domain abstractions to messaging and test
infrastructure — so you can adopt only what you need.

## Packages

Adopt only what you need — every package is independently versioned and focused on a single concern.

**Core**

| Package | Description |
|---|---|
| **Vulthil.Results** | Result primitives for explicit success/failure flows without exceptions. |
| **Vulthil.SharedKernel** | Domain primitives — aggregate roots, entities, domain events, and domain exceptions. |

**Application & API**

| Package | Description |
|---|---|
| **Vulthil.SharedKernel.Application** | CQRS handlers, pipeline behaviors, and FluentValidation integration. |
| **Vulthil.SharedKernel.Api** | Minimal-API endpoint helpers and `Result`-to-HTTP conversion. |

**Persistence & Outbox**

| Package | Description |
|---|---|
| **Vulthil.SharedKernel.Outbox** | Transactional domain-event outbox engine; persistence-agnostic. |
| **Vulthil.SharedKernel.Outbox.EntityFrameworkCore** | EF Core implementation of the domain-event outbox. |
| **Vulthil.SharedKernel.Infrastructure** | EF Core persistence, transactions, and outbox wiring. |
| **Vulthil.SharedKernel.Infrastructure.Relational** | Shared relational helpers for the provider packages. |
| **Vulthil.SharedKernel.Infrastructure.Npgsql** | PostgreSQL/Npgsql EF Core mappings and optimizations. |
| **Vulthil.SharedKernel.Infrastructure.MySql** | MySQL EF Core mappings and optimizations. |
| **Vulthil.SharedKernel.Infrastructure.Cosmos** | Azure Cosmos DB EF Core mappings and optimizations. |

**Messaging**

| Package | Description |
|---|---|
| **Vulthil.Messaging.Abstractions** | Contracts for producers, consumers, and request/reply boundaries. |
| **Vulthil.Messaging** | Composition APIs for consumers, queues, and hosted processing. |
| **Vulthil.Messaging.RabbitMq** | RabbitMQ transport for the messaging abstractions. |
| **Vulthil.Messaging.Outbox** | Transactional bus-publish outbox that eliminates the dual-write problem. |
| **Vulthil.Messaging.Inbox** | Idempotent-receiver (inbox) consume filter for exactly-once processing. |
| **Vulthil.Messaging.Inbox.EntityFrameworkCore** | Shared EF Core primitives for the inbox idempotency store. |
| **Vulthil.Messaging.Inbox.Relational** | Relational EF Core idempotency store (transactional exactly-once). |
| **Vulthil.Messaging.Inbox.Cosmos** | Azure Cosmos DB idempotency store (effectively-once). |
| **Vulthil.Messaging.TestHarness** | In-memory harness for asserting messaging flows in tests. |

**Hosting & Testing**

| Package | Description |
|---|---|
| **Vulthil.Extensions.Hosting** | Hosting abstractions, including `IRestartableHostedService`. |
| **Vulthil.Extensions.Testing** | Testing helpers such as polling utilities for eventual consistency. |
| **Vulthil.xUnit** | xUnit base classes with Testcontainers, Respawn, and AutoMocker. |
| **Vulthil.xUnit.Cosmos** | Cosmos DB emulator fixture for xUnit integration tests. |

## Documentation

Full documentation is available on the [documentation site](https://vulthil.github.io/Vulthil.SharedKernel/),
with conceptual guides and usage patterns in the [docs/articles](docs/articles) folder and
a complete [API reference](docs/api).

## Contributing

Contributions are welcome. Please read the [Contributing Guide](.github/CONTRIBUTING.md)
to get started before opening an issue or pull request.

- 🐛 [Report a bug](https://github.com/Vulthil/Vulthil.SharedKernel/issues/new?template=bug_report.yml)
- 💡 [Request a feature](https://github.com/Vulthil/Vulthil.SharedKernel/issues/new?template=feature_request.yml)
- 🔒 [Report a security vulnerability](.github/SECURITY.md)

## License

This project is licensed under the [MIT License](LICENSE).
