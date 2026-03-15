# Vulthil.SharedKernel

[![Build and Test](https://github.com/Vulthil/Vulthil.SharedKernel/actions/workflows/ci.yml/badge.svg)](https://github.com/Vulthil/Vulthil.SharedKernel/actions/workflows/ci.yml)

A collection of opinionated .NET building blocks for domain-driven design, CQRS, messaging, and testing.

## Packages

| Package | Description |
|---|---|
| **Vulthil.Results** | Small result primitives for explicit success/failure flows without exceptions. |
| **Vulthil.SharedKernel** | Core domain primitives and base abstractions for shared domain logic. |
| **Vulthil.SharedKernel.Application** | Application-layer building blocks such as handlers, pipelines, and validation helpers. |
| **Vulthil.SharedKernel.Infrastructure** | Infrastructure helpers for persistence, transactions, outbox, and EF Core integration. |
| **Vulthil.SharedKernel.Api** | API-layer helpers for endpoints, controllers, and cross-cutting HTTP concerns. |
| **Vulthil.Messaging.Abstractions** | Messaging contracts for producers/consumers and request/reply boundaries. |
| **Vulthil.Messaging** | Messaging composition APIs for configuring consumers, queues, and hosted processing. |
| **Vulthil.Messaging.RabbitMq** | RabbitMQ implementation for the Vulthil messaging abstractions. |
| **Vulthil.Messaging.TestHarness** | Test utilities for validating messaging flows in integration and component tests. |
| **Vulthil.Extensions.Testing** | Testing-oriented extensions for asserting and composing application behaviors. |
| **Vulthil.xUnit** | Reusable xUnit base infrastructure for integration and unit test composition. |

## Quick Start

```bash
dotnet add package Vulthil.Results
dotnet add package Vulthil.SharedKernel
dotnet add package Vulthil.SharedKernel.Application
```

## Documentation

Full documentation is available at the [docs site](https://vulthil.github.io/Vulthil.SharedKernel/) and in the [docs/articles](docs/articles) folder.

## Testing

```bash
dotnet test
```

## License

This project is licensed under the [MIT License](LICENSE).