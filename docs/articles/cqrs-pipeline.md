# CQRS & Pipeline Behaviors

`Vulthil.SharedKernel.Application` implements the CQRS (Command Query Responsibility Segregation) pattern with a lightweight in-process sender and configurable pipeline behaviors.

## Concepts

| Abstraction | Description |
|---|---|
| `ICommand` / `ICommand<TResponse>` | Write operation that changes state |
| `ITransactionalCommand` / `ITransactionalCommand<TResponse>` | Command executed inside a database transaction |
| `IQuery<TResponse>` | Read operation that returns data |
| `ICommandHandler<TCommand, TResponse>` | Handles a command |
| `IQueryHandler<TQuery, TResponse>` | Handles a query |
| `ISender` | Dispatches requests to their handlers |
| `IPipelineHandler<TRequest, TResponse>` | Cross-cutting middleware for the handler pipeline |

## Registration

```csharp
builder.Services.AddApplication(options =>
{
    // Scan assemblies for handlers
    options.RegisterHandlerAssemblies(typeof(Program).Assembly);

    // Scan assemblies for FluentValidation validators
    options.RegisterFluentValidationAssemblies(typeof(Program).Assembly);

    // Add pipeline behaviors (order matters)
    options.AddValidationPipelineBehavior();
    options.AddRequestLoggingBehavior();
    options.AddTransactionalPipelineBehavior();
});
```

## Defining Commands and Handlers

```csharp
public sealed record CreateUserCommand(string Email) : ICommand<Result<Guid>>;

public sealed class CreateUserCommandHandler(IUserRepository users, IUnitOfWork uow)
    : ICommandHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(
        CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = User.Create(request.Email);
        users.Add(user);
        await uow.SaveChangesAsync(cancellationToken);
        return user.Id.Value;
    }
}
```

## Defining Queries and Handlers

```csharp
public sealed record GetUserQuery(Guid UserId) : IQuery<Result<UserDto>>;

public sealed class GetUserQueryHandler(AppDbContext db)
    : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> HandleAsync(
        GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Where(u => u.Id == new UserId(request.UserId))
            .Select(u => new UserDto(u.Id.Value, u.Email))
            .FirstOrDefaultAsync(cancellationToken);

        return user is not null
            ? Result.Success(user)
            : Result.Failure<UserDto>(UserErrors.NotFound);
    }
}
```

## Pipeline Behaviors

Pipeline behaviors wrap every handler invocation, allowing you to add cross-cutting logic without modifying individual handlers.

### Validation

`ValidationPipelineBehavior` runs all registered `IValidator<TCommand>` instances before the handler executes. When validation fails, it short-circuits and returns a `Result` containing a `ValidationError`:

```csharp
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
```

No additional wiring is needed – the validator is picked up automatically from the registered assemblies.

### Request Logging

`RequestLoggingPipelineBehavior` logs the start and completion of every request, attaching the serialised error to the logging scope when the result is a failure.

### Transactional

`TransactionalPipelineBehavior` wraps `ITransactionalCommand` handlers inside a database transaction. If the handler succeeds, the transaction commits; if it throws, the transaction rolls back.

```csharp
// Mark a command as transactional
public sealed record TransferFundsCommand(Guid FromAccount, Guid ToAccount, decimal Amount)
    : ITransactionalCommand<Result>;
```

## Dispatching Requests

Inject `ISender` into your API endpoints or controllers:

```csharp
app.MapPost("/users", async (CreateUserCommand command, ISender sender) =>
{
    var result = await sender.SendAsync(command);
    return result.ToIResult();
});
```

### Direct handler injection

You can also inject the handler interface directly. The resolved instance is the same pipeline-wrapped handler that `ISender` would dispatch to, so behaviors (validation, logging, transactions, custom ones) apply uniformly either way.

```csharp
app.MapPost("/users", async (
    CreateUserCommand command,
    ICommandHandler<CreateUserCommand, Result<Guid>> handler) =>
{
    var result = await handler.HandleAsync(command);
    return result.ToIResult();
});
```

The interfaces you can inject are `IHandler<TRequest, TResponse>`, `ICommandHandler<TCommand, TResponse>`, `ICommandHandler<TCommand>` (for `Result`-returning commands) and `IQueryHandler<TQuery, TResponse>`. The concrete handler implementation is *not* registered as a DI service — there is no way to bypass the pipeline by injecting the concrete type.

## Behaviors across assemblies

Pipeline behaviors are composed at handler-resolution time, not at registration time, so the order of registration is irrelevant. Different assemblies may register handlers and behaviors independently — all behaviors registered before `BuildServiceProvider` apply to all handlers resolved afterwards.

```csharp
// In an Infrastructure assembly:
services.AddOpenPipelineHandler(typeof(MyCustomBehavior<,>));
```
