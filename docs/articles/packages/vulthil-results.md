# Vulthil.Results

Use `Vulthil.Results` to model success/failure explicitly without exceptions.

## When to use

- Returning validation/business errors without exceptions
- Chaining operations with predictable control flow

## Pattern

- Return `Result`/`Result<T>` from application logic
- Map infrastructure/domain failures to `Error` values
- Keep controllers/endpoints responsible for HTTP mapping

## Usage

### Creating results

```csharp
// Success
Result result = Result.Success();
Result<int> typed = Result.Success(42);

// Failure
Error error = Error.NotFound("User.NotFound", "User was not found");
Result<User> failed = Result.Failure<User>(error);
```

### Defining domain errors

```csharp
public static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("User.NotFound", "User was not found");

    public static readonly Error EmailTaken =
        Error.Conflict("User.EmailTaken", "Email is already in use");
}
```

### Chaining with Bind, Map, Tap, and Match

```csharp
// Bind chains operations that return Result
Result<Order> order = await GetUser(userId)
    .BindAsync(user => CreateOrder(user));

// Map transforms the success value
Result<string> name = GetUser(userId)
    .Map(user => user.Name);

// Tap runs a side-effect without changing the result
Result<User> user = await GetUser(userId)
    .TapAsync(u => LogAccess(u));

// Match folds into a single value
string message = result.Match(
    onSuccess: () => "OK",
    onFailure: error => error.Description);
```

### Converting nullable values

```csharp
User? user = await repository.GetByIdAsync(id);
Result<User> result = user.ToResult(UserErrors.NotFound);
```
