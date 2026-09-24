# Vulthil.SharedKernel.Api

Use `Vulthil.SharedKernel.Api` to standardize API endpoint composition.

## When to use

- Endpoint/controller base abstractions
- API-layer extension methods and conventions

## Pattern

- Keep transport concerns in API layer
- Translate `Result` values to HTTP responses centrally
- Reuse endpoint conventions across services

## Usage

### Minimal API endpoints

```csharp
public sealed class GetUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/users/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.SendAsync(new GetUserQuery(id));
            return result.ToIResult();
        })
        .ProducesErrors(ErrorType.NotFound);
    }
}
```

`ToIResult()` returns `Results<Ok<T>, ProblemHttpResult>`: the success member documents itself, and every failure is
returned as one problem response (RFC 7807) with the status mapped from the `ErrorType` and the error's description
as `detail`. Declare the errors an endpoint can produce with `ProducesErrors(...)` (or `[ProducesError(...)]`), in
error terms, and OpenAPI documents exactly those problem responses plus a `500` on every operation. See
[Result Pattern — Mapping to HTTP Responses](../result-pattern.md#mapping-to-http-responses).

### Registration and mapping

```csharp
// In Program.cs
builder.Services.AddEndpoints(typeof(Program).Assembly);
builder.Services.AddOpenApiServices();

var app = builder.Build();

app.MapEndpoints();
app.MapOpenApiEndpoints();
```

### Controller-based endpoints

Derive from `BaseController` for the standard `[ApiController]`/route conventions and a `Logger` property resolved for the concrete controller type (no constructor logger argument required). Controllers can return typed results directly for OpenAPI documentation:

```csharp
public sealed class UsersController(ISender sender) : BaseController
{
    [HttpGet("{id:guid}")]
    [ProducesError(ErrorType.NotFound)]
    public async Task<Results<Ok<UserDto>, ProblemHttpResult>> Get(Guid id)
    {
        var result = await sender.SendAsync(new GetUserQuery(id));
        return result.ToIResult();
    }
}
```

Or use `IActionResult` with model-state error translation; `[ProducesError]` documents the problem responses here too:

```csharp
public sealed class UsersController(ISender sender) : BaseController
{
    [HttpGet("{id:guid}")]
    [ProducesError(ErrorType.NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await sender.SendAsync(new GetUserQuery(id));
        return result.ToActionResult(this);
    }
}
```
