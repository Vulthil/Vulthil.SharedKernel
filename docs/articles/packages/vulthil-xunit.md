# Vulthil.xUnit

Use `Vulthil.xUnit` for reusable xUnit test base infrastructure.

## When to use

- Shared base classes for unit/integration tests
- Consistent setup, teardown, and factory/container integration

## Pattern

- Derive from shared base test classes consistently
- Keep fixture setup centralized and reusable
- Keep test classes focused on behavior, not plumbing

This package is xUnit-coupled by design; the framework-agnostic helpers (`Result`-based polling, HTTP response
deserialization) live in [Vulthil.Extensions.Testing](vulthil-extensions-testing.md).

See [Testing](../testing.md) for the full guide (base classes, containers, `ContainerHost`, HTTP mocks).
