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
