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
        });
    }
}
```

`ToIResult()` returns `Results<Ok<T>, ValidationProblem, NotFound, Conflict, ProblemHttpResult>`, so OpenAPI automatically documents all possible response types.

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
    public async Task<Results<Ok<UserDto>, ValidationProblem, NotFound, Conflict, ProblemHttpResult>> Get(Guid id)
    {
        var result = await sender.SendAsync(new GetUserQuery(id));
        return result.ToIResult();
    }
}
```

Or use `IActionResult` with model-state error translation:

```csharp
public sealed class UsersController(ISender sender) : BaseController
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await sender.SendAsync(new GetUserQuery(id));
        return result.ToActionResult(this);
    }
}
```
