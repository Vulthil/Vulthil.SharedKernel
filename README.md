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

| Package | NuGet | Description |
|---|---|---|
| **Vulthil.Results** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Results)](https://www.nuget.org/packages/Vulthil.Results) | Result primitives for explicit success/failure flows without exceptions. |
| **Vulthil.SharedKernel** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel)](https://www.nuget.org/packages/Vulthil.SharedKernel) | Domain primitives — aggregate roots, entities, domain events, and domain exceptions. |

**Application & API**

| Package | NuGet | Description |
|---|---|---|
| **Vulthil.SharedKernel.Application** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Application)](https://www.nuget.org/packages/Vulthil.SharedKernel.Application) | CQRS handlers, pipeline behaviors, and FluentValidation integration. |
| **Vulthil.SharedKernel.Api** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Api)](https://www.nuget.org/packages/Vulthil.SharedKernel.Api) | Minimal-API endpoint helpers and `Result`-to-HTTP conversion. |

**Persistence & Outbox**

| Package | NuGet | Description |
|---|---|---|
| **Vulthil.SharedKernel.Outbox** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Outbox)](https://www.nuget.org/packages/Vulthil.SharedKernel.Outbox) | Transactional domain-event outbox engine; persistence-agnostic. |
| **Vulthil.SharedKernel.Outbox.EntityFrameworkCore** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Outbox.EntityFrameworkCore)](https://www.nuget.org/packages/Vulthil.SharedKernel.Outbox.EntityFrameworkCore) | EF Core implementation of the domain-event outbox. |
| **Vulthil.SharedKernel.Infrastructure** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Infrastructure)](https://www.nuget.org/packages/Vulthil.SharedKernel.Infrastructure) | EF Core persistence, transactions, and outbox wiring. |
| **Vulthil.SharedKernel.Infrastructure.Relational** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Infrastructure.Relational)](https://www.nuget.org/packages/Vulthil.SharedKernel.Infrastructure.Relational) | Shared relational helpers for the provider packages. |
| **Vulthil.SharedKernel.Infrastructure.Npgsql** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Infrastructure.Npgsql)](https://www.nuget.org/packages/Vulthil.SharedKernel.Infrastructure.Npgsql) | PostgreSQL/Npgsql EF Core mappings and optimizations. |
| **Vulthil.SharedKernel.Infrastructure.MySql** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Infrastructure.MySql)](https://www.nuget.org/packages/Vulthil.SharedKernel.Infrastructure.MySql) | MySQL EF Core mappings and optimizations. |
| **Vulthil.SharedKernel.Infrastructure.Cosmos** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.SharedKernel.Infrastructure.Cosmos)](https://www.nuget.org/packages/Vulthil.SharedKernel.Infrastructure.Cosmos) | Azure Cosmos DB EF Core mappings and optimizations. |

**Messaging**

| Package | NuGet | Description |
|---|---|---|
| **Vulthil.Messaging.Abstractions** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.Abstractions)](https://www.nuget.org/packages/Vulthil.Messaging.Abstractions) | Contracts for producers, consumers, and request/reply boundaries. |
| **Vulthil.Messaging** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging)](https://www.nuget.org/packages/Vulthil.Messaging) | Composition APIs for consumers, queues, and hosted processing. |
| **Vulthil.Messaging.RabbitMq** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.RabbitMq)](https://www.nuget.org/packages/Vulthil.Messaging.RabbitMq) | RabbitMQ transport for the messaging abstractions. |
| **Vulthil.Messaging.Outbox** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.Outbox)](https://www.nuget.org/packages/Vulthil.Messaging.Outbox) | Transactional bus-publish outbox that eliminates the dual-write problem. |
| **Vulthil.Messaging.Inbox** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.Inbox)](https://www.nuget.org/packages/Vulthil.Messaging.Inbox) | Idempotent-receiver (inbox) consume filter for exactly-once processing. |
| **Vulthil.Messaging.Inbox.EntityFrameworkCore** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.Inbox.EntityFrameworkCore)](https://www.nuget.org/packages/Vulthil.Messaging.Inbox.EntityFrameworkCore) | Shared EF Core primitives for the inbox idempotency store. |
| **Vulthil.Messaging.Inbox.Relational** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.Inbox.Relational)](https://www.nuget.org/packages/Vulthil.Messaging.Inbox.Relational) | Relational EF Core idempotency store (transactional exactly-once). |
| **Vulthil.Messaging.Inbox.Cosmos** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.Inbox.Cosmos)](https://www.nuget.org/packages/Vulthil.Messaging.Inbox.Cosmos) | Azure Cosmos DB idempotency store (effectively-once). |
| **Vulthil.Messaging.TestHarness** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.TestHarness)](https://www.nuget.org/packages/Vulthil.Messaging.TestHarness) | In-memory harness for asserting messaging flows in tests. |

**Hosting & Testing**

| Package | NuGet | Description |
|---|---|---|
| **Vulthil.Extensions.Hosting** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Extensions.Hosting)](https://www.nuget.org/packages/Vulthil.Extensions.Hosting) | Hosting abstractions, including `IRestartableHostedService`. |
| **Vulthil.Extensions.Testing** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.Extensions.Testing)](https://www.nuget.org/packages/Vulthil.Extensions.Testing) | Testing helpers such as polling utilities for eventual consistency. |
| **Vulthil.xUnit** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.xUnit)](https://www.nuget.org/packages/Vulthil.xUnit) | xUnit base classes with Testcontainers, Respawn, and AutoMocker. |
| **Vulthil.xUnit.Cosmos** | [![NuGet](https://img.shields.io/nuget/v/Vulthil.xUnit.Cosmos)](https://www.nuget.org/packages/Vulthil.xUnit.Cosmos) | Cosmos DB emulator fixture for xUnit integration tests. |

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
