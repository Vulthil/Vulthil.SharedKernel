# Vulthil.SharedKernel

Use `Vulthil.SharedKernel` for domain primitives reused across bounded contexts.

## When to use

- Entities, aggregate roots, and domain event abstractions
- Shared domain exceptions and base contracts

## Pattern

- Keep business invariants inside domain types
- Raise domain events from aggregate roots
- Keep domain model independent from infrastructure concerns

## Usage

### Defining an entity

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
}
```

### Defining a domain event

```csharp
public sealed record UserCreatedEvent(UserId UserId) : IDomainEvent;
```

### Handling a domain event

```csharp
public sealed class UserCreatedEventHandler : IDomainEventHandler<UserCreatedEvent>
{
    public Task HandleAsync(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        // React to user creation
        return Task.CompletedTask;
    }
}
```

### Domain exceptions

`DomainException` is abstract and takes an `Error` — derive a concrete exception per invariant:

```csharp
public sealed class UserNotFoundException : DomainException
{
    public UserNotFoundException(UserId userId)
        : base(Error.NotFound("User.NotFound", $"User {userId.Value} was not found"))
    { }
}
```
