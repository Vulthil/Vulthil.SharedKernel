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
    Error.Validation("Email.Required", "Email is required"),
    Error.Validation("Name.TooLong", "Name exceeds 100 characters")
]);
Result<User> invalid = Result.ValidationFailure<User>(validation);
```

## Error Classifications

`ErrorType` determines how the error maps to HTTP status codes in the API layer:

| ErrorType | Factory Method | HTTP Status | Meaning |
|---|---|---|---|
| `Failure` | `Error.Failure(...)` | 500 Internal Server Error | Unclassified/unexpected failure |
| `NotFound` | `Error.NotFound(...)` | 404 Not Found | The requested resource doesn't exist |
| `Problem` | `Error.Problem(...)` | 400 Bad Request | Client-addressable business-rule violation; full error detail is returned |
| `Conflict` | `Error.Conflict(...)` | 409 Conflict | The request conflicts with current state |
| `Validation` | `Error.Validation(...)` / `ValidationError` | 400 Bad Request | Input validation failure; `ValidationError.Errors` carries the per-field details |

`Error.Validation` mints the *inner* errors of a `ValidationError` (this is what `ValidationPipelineBehavior` uses for
FluentValidation failures). `Error.Problem` is for a single, standalone business-rule error — it is a different
classification from `Validation`, not a supertype of it.

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

### Combine / Zip – aggregate multiple results

`Combine` (a sequence of `Result`) and `Zip` (exactly two `Result<T>`) both follow the same aggregation rule:

- **All succeed** → success.
- **Exactly one fails** → that failure's **original error** propagates unwrapped. Combining a single `NotFound`
  failure still surfaces as `NotFound`, not as a validation failure.
- **More than one fails** → the failures are aggregated into a single `ValidationError` whose `Errors` list holds
  every failed error in order.

```csharp
Result combined = ResultExtensions.Combine(CheckName(), CheckEmail(), CheckAge());
// combined.Error is the original NotFound/Conflict/etc. error if only one check failed,
// or a ValidationError aggregating all of them if more than one failed.

Result<(User, Order)> zipped = GetUser(userId).Zip(GetOrder(orderId));
// same rule: a single failure propagates as-is; two failures aggregate into a ValidationError.
```

## Sharp Edges

A few behaviors are easy to trip over. They are intentional for now; genuinely breaking fixes (marked below) are
deferred to a future major version.

- **Implicit `TValue? → Result<TValue>` conversion swallows `null` as a failure.** Assigning a `null` value to a
  `Result<TValue>` compiles and silently produces a *failed* result carrying `Error.NullValue` — the conversion
  picks the error for you, and there is no compiler warning at the call site. Prefer constructing the result
  explicitly (`Result.Failure<TValue>(someDomainError)`) when the caller should choose the error. *(Making this
  conversion explicit is a tracked future breaking change.)*
- **`Result` and `Result<T>` have no value equality.** Both are plain classes with no `Equals`/`GetHashCode`
  override, so two results are only equal by reference — two separately-constructed successes (or two
  identically-failed results) are never `Equals`-equal. Compare `IsSuccess`/`Error`/`Value` instead of the result
  itself. *(Adding value equality is a tracked future breaking change.)*
- **A custom error that "looks like" `Error.None` is a valid failure error.** The `Result` constructor rejects a
  failure whose error is literally the `Error.None` singleton, but a custom error with the same empty code and
  description (e.g. `Error.Failure(string.Empty, string.Empty)`) is a distinct object and is accepted — it is not
  treated as "no error" just because its field values match.

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
