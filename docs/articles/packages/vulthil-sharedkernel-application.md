# Vulthil.SharedKernel.Application

Use `Vulthil.SharedKernel.Application` for application orchestration concerns.

## When to use

- Commands/queries and their handlers
- Pipeline handlers and cross-cutting behaviors
- FluentValidation integration

## Pattern

- Keep handlers thin and focused on orchestration
- Delegate business rules to domain services/entities
- Use pipeline components for validation/logging/transactions

## Usage

### Registration

```csharp
services.AddApplication(options =>
{
    options.RegisterHandlerAssemblies(typeof(Program).Assembly);
    options.RegisterFluentValidationAssemblies(typeof(Program).Assembly);
    options.AddValidationPipelineBehavior();
    options.AddRequestLoggingBehavior();
    options.AddTransactionalPipelineBehavior();
});
```

### Defining a command and handler

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

### Defining a query and handler

```csharp
public sealed record GetUserQuery(Guid UserId) : IQuery<Result<UserDto>>;

public sealed class GetUserQueryHandler(IUserRepository users)
    : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> HandleAsync(
        GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(new UserId(request.UserId), cancellationToken);
        return user.ToResult(UserErrors.NotFound)
            .Map(u => new UserDto(u.Id.Value, u.Email));
    }
}
```

### FluentValidation with Error integration

```csharp
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithError(UserErrors.EmailRequired);
    }
}
```

### Sending requests

```csharp
var result = await sender.SendAsync(new CreateUserCommand("user@example.com"), cancellationToken);
```

### Direct handler injection

`ICommandHandler<TCommand, TResponse>`, `ICommandHandler<TCommand>`, `IQueryHandler<TQuery, TResponse>` and `IHandler<TRequest, TResponse>` can also be injected directly. The resolved instance shares the same pipeline as `ISender`, so any registered behavior (validation, logging, transactions, custom) still applies.

```csharp
public sealed class CreateUserEndpoint(ICommandHandler<CreateUserCommand, Result<Guid>> handler)
{
    public Task<Result<Guid>> ExecuteAsync(CreateUserCommand command, CancellationToken ct)
        => handler.HandleAsync(command, ct);
}
```

Custom behaviors registered from any assembly with `services.AddOpenPipelineHandler(typeof(MyBehavior<,>))` apply to every handler resolved afterwards — order of registration relative to `AddApplication` does not matter.
