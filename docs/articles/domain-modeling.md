# Domain Modeling

`Vulthil.SharedKernel` provides base types for building a rich domain model following Domain-Driven Design conventions.

## Primitives

| Type | Description |
|---|---|
| `Entity<TId>` | Base class for entities identified by a strongly-typed ID |
| `AggregateRoot<TId>` | Entity subclass that tracks and raises domain events |
| `IDomainEvent` | Marker interface for domain events |
| `IDomainEventHandler<T>` | Handler contract for processing domain events |
| `DomainException` | Base exception for domain invariant violations |

## Defining an Entity

Entities are compared by identity, not by attribute values:

```csharp
public sealed class UserId(Guid value)
{
    public Guid Value { get; } = value;
}

public sealed class User : AggregateRoot<UserId>
{
    public string Email { get; private set; }

    private User(UserId id, string email) : base(id)
    {
        Email = email;
    }

    public static User Create(string email)
    {
        var user = new User(new UserId(Guid.NewGuid()), email);
        user.Raise(new UserCreatedEvent(user.Id));
        return user;
    }

    public void ChangeEmail(string newEmail)
    {
        Email = newEmail;
        Raise(new UserEmailChangedEvent(Id, newEmail));
    }
}
```

Key points:

- The constructor is **private** – creation goes through a factory method that enforces invariants.
- `Raise()` records a domain event without immediately dispatching it.
- Events are collected and published later (typically when the unit of work commits).

## Domain Events

Domain events capture something meaningful that happened in the domain:

```csharp
public sealed record UserCreatedEvent(UserId UserId) : IDomainEvent;
public sealed record UserEmailChangedEvent(UserId UserId, string NewEmail) : IDomainEvent;
```

### Handling Domain Events

```csharp
public sealed class UserCreatedEventHandler : IDomainEventHandler<UserCreatedEvent>
{
    public Task HandleAsync(UserCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Send welcome email, update read model, etc.
        return Task.CompletedTask;
    }
}
```

Domain event handlers are discovered automatically when you register the application layer with `AddApplication`.

## Domain Events and the Outbox

When `Vulthil.SharedKernel.Infrastructure` is configured with outbox processing, domain events raised by aggregate roots are serialised into `OutboxMessage` rows during `SaveChangesAsync`. A background service then publishes them, guaranteeing at-least-once delivery even if the process crashes after the database commit.

See [Outbox Pattern](outbox-pattern.md) for configuration details.

## Protecting Invariants

Use `DomainException` for invariant violations that represent programming errors or impossible states rather than expected business failures:

```csharp
public void Deactivate()
{
    if (!IsActive)
    {
        throw new DomainException("User is already inactive.");
    }

    IsActive = false;
}
```

For expected business failures (e.g. "email already taken"), prefer returning a `Result` with an `Error` instead. See [Result Pattern](result-pattern.md).
