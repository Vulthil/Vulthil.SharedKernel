# Result Pattern

`Vulthil.Results` provides a railway-oriented `Result` / `Result<T>` type that models success and failure explicitly, eliminating the need to throw exceptions for expected error conditions.

## Core Types

| Type | Description |
|---|---|
| `Result` | Represents success or failure without a value |
| `Result<T>` | Represents success with a value of type `T`, or failure |
| `Error` | Structured error with `Code`, `Description`, and `ErrorType` |
| `ValidationError` | An `Error` subtype that carries multiple inner errors |

## Creating Results

```csharp
// Success
Result ok = Result.Success();
Result<int> typed = Result.Success(42);

// Failure
Error error = Error.NotFound("User.NotFound", "User was not found");
Result<User> failed = Result.Failure<User>(error);

// Validation failure
var validation = new ValidationError([
    Error.Problem("Email.Required", "Email is required"),
    Error.Problem("Name.TooLong", "Name exceeds 100 characters")
]);
Result<User> invalid = Result.ValidationFailure<User>(validation);
```

## Error Classifications

`ErrorType` determines how the error maps to HTTP status codes in the API layer:

| ErrorType | Factory Method | HTTP Status |
|---|---|---|
| `Failure` | `Error.Failure(...)` | 500 Internal Server Error |
| `NotFound` | `Error.NotFound(...)` | 404 Not Found |
| `Problem` | `Error.Problem(...)` | 500 Internal Server Error |
| `Conflict` | `Error.Conflict(...)` | 409 Conflict |
| `Validation` | `ValidationError(...)` | 400 Bad Request |

## Defining Domain Errors

Keep domain errors as static fields so they are reusable and discoverable:

```csharp
public static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("User.NotFound", "User was not found");

    public static readonly Error EmailTaken =
        Error.Conflict("User.EmailTaken", "Email is already in use");
}
```

## Functional Extensions

The library ships extension methods that let you compose operations without manual `if`/`else` branching.

### Bind – chain dependent operations

```csharp
Result<Order> result = await GetUser(userId)
    .BindAsync(user => CreateOrder(user));
```

If `GetUser` fails, the error propagates and `CreateOrder` is never called.

### Map – transform the success value

```csharp
Result<string> email = getUser
    .Map(user => user.Email);
```

### Tap – execute a side-effect without changing the result

```csharp
Result<User> result = await CreateUser(command)
    .TapAsync(user => logger.LogInformation("Created {Id}", user.Id));
```

### Match – branch on success/failure

```csharp
string message = result.Match(
    onSuccess: user => $"Welcome, {user.Name}",
    onFailure: error => $"Error: {error.Description}");
```

All extensions have synchronous and asynchronous overloads so they compose naturally with `Task<Result<T>>`.

## Mapping to HTTP Responses

`Vulthil.SharedKernel.Api` provides helpers that turn a `Result` into the correct HTTP response with typed results for OpenAPI documentation:

```csharp
// Minimal API endpoint – returns Results<Ok<UserDto>, ValidationProblem, NotFound, Conflict, ProblemHttpResult>
app.MapGet("/users/{id:guid}", async (Guid id, ISender sender) =>
{
    var result = await sender.SendAsync(new GetUserQuery(id));
    return result.ToIResult();
});

// Controller-based endpoint with typed results
public async Task<Results<Ok<UserDto>, ValidationProblem, NotFound, Conflict, ProblemHttpResult>> Get(Guid id)
{
    var result = await _sender.SendAsync(new GetUserQuery(id));
    return result.ToIResult();
}

// Controller-based endpoint with IActionResult
public async Task<IActionResult> Get(Guid id)
{
    var result = await _sender.SendAsync(new GetUserQuery(id));
    return result.ToActionResult(this);
}
```
