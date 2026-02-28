# Getting Started

**Vulthil.SharedKernel** is a collection of NuGet packages that provide building blocks for .NET applications following Domain-Driven Design, CQRS, and clean architecture principles.

## Architecture Overview

The packages are organised into layers so you can adopt only what you need:

| Layer | Package | Purpose |
|---|---|---|
| Domain | `Vulthil.SharedKernel` | Entity, aggregate root, and domain event primitives |
| Domain | `Vulthil.Results` | Railway-oriented `Result<T>` for explicit error handling |
| Application | `Vulthil.SharedKernel.Application` | Commands, queries, handlers, and pipeline behaviors |
| Infrastructure | `Vulthil.SharedKernel.Infrastructure` | EF Core base context, generic repository, and outbox processing |
| API | `Vulthil.SharedKernel.Api` | Minimal API endpoint conventions and `Result` → HTTP mapping |
| Messaging | `Vulthil.Messaging.Abstractions` | Transport-agnostic consumer and publisher contracts |
| Messaging | `Vulthil.Messaging` | Queue/consumer registration and hosted service orchestration |
| Messaging | `Vulthil.Messaging.RabbitMq` | RabbitMQ transport implementation |
| Testing | `Vulthil.xUnit` | Base test classes, auto-mocking, and Testcontainers integration |
| Testing | `Vulthil.Messaging.TestHarness` | In-memory messaging test harness |
| Testing | `Vulthil.Extensions.Testing` | Shared assertion and setup helpers |

## Minimal Example

Below is a condensed example showing the typical setup of a Web API project that uses the core packages:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Application layer – registers handlers, validators, and pipeline behaviors
builder.Services.AddApplication(options =>
{
    options.RegisterHandlerAssemblies(typeof(Program).Assembly);
    options.RegisterFluentValidationAssemblies(typeof(Program).Assembly);
    options.AddValidationPipelineBehavior();
});

// Infrastructure layer – EF Core context with outbox
builder.Services.AddDbContext<AppDbContext>(config =>
{
    config.ConfigureDbContextOptions(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
    config.EnableOutboxProcessing();
});

// API layer – endpoint discovery
builder.Services.AddEndpoints(typeof(Program).Assembly);

var app = builder.Build();
app.MapEndpoints();
app.Run();
```

## Next Steps

- [Result Pattern](result-pattern.md) – explicit success/failure handling
- [Domain Modeling](domain-modeling.md) – entities, aggregates, and events
- [CQRS & Pipeline Behaviors](cqrs-pipeline.md) – commands, queries, and cross-cutting concerns
- [Messaging](messaging.md) – asynchronous messaging with queues and consumers
- [Outbox Pattern](outbox-pattern.md) – reliable domain event delivery
- [Testing](testing.md) – unit and integration testing infrastructure
