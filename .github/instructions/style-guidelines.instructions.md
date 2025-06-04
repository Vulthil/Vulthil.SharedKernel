---
apply-to: "**/*.cs"
---

## Namespaces
- Use file-scoped namespaces that match the directory structure.

## Immutability
- Prefer immutable types unless mutability is requested.
- Prefer records for immutable types.

## Files Organization
- Define one type per file.

## Record Design
- Define record's properties on the same line as the record declaration.
- Use immutable collections in records unless requested otherwise.
- Use `ImmutableList<T>` in records whenever possible.
- Define record behavior in extension methods in other static classes.

## Discriminated Unions Design
- Prefer using records for discriminated unions.
- Derive specific types from a base abstract record.
- Define the entire discriminated union in one file.
- Define one static factory class per discriminated union.
- Expose one static factory method per variant.
- Follow all rules for record design when designing a discriminated union.

## Domain Models Design
- Use a private constructor for domain models.
- Properties should be defined as `get; private set;`.
- Collections should be defined as `IReadOnlyCollection<T>` or `IReadOnlyList<T>` and have a private backing field.
- Expose static `Create` method in the domain class for creating instances of the domain model.
- Place argument validation in the `Create` method.