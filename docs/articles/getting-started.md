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
| Infrastructure | `Vulthil.SharedKernel.Outbox` | EF-free transactional outbox engine (relay, dispatchers, `IOutboxStore`) |
| Infrastructure | `Vulthil.SharedKernel.Outbox.EntityFrameworkCore` | EF Core implementation of the outbox engine |
| Infrastructure | `Vulthil.SharedKernel.Infrastructure.Relational` | Shared relational support and the relational outbox store base |
| Infrastructure | `Vulthil.SharedKernel.Infrastructure.Npgsql` | PostgreSQL provider integration (`UseNpgsql`) |
| Infrastructure | `Vulthil.SharedKernel.Infrastructure.MySql` | MySQL provider integration (`UseMySql`) |
| Infrastructure | `Vulthil.SharedKernel.Infrastructure.Cosmos` | Azure Cosmos DB provider integration (`UseCosmosDb`) |
| API | `Vulthil.SharedKernel.Api` | Minimal API endpoint conventions and `Result` → HTTP mapping |
| Hosting | `Vulthil.Extensions.Hosting` | `IRestartableHostedService` marker for cleanly pausable hosted services |
| Messaging | `Vulthil.Messaging.Abstractions` | Transport-agnostic consumer and publisher contracts |
| Messaging | `Vulthil.Messaging` | Queue/consumer registration, hosted orchestration, and the transport SDK |
| Messaging | `Vulthil.Messaging.RabbitMq` | RabbitMQ transport implementation |
| Messaging | `Vulthil.Messaging.Outbox` | Transactional bus-publish outbox (captures publishes into the outbox) |
| Messaging | `Vulthil.Messaging.Inbox` | Idempotent-receiver consume filter for exactly/effectively-once processing |
| Messaging | `Vulthil.Messaging.Inbox.EntityFrameworkCore` | Shared EF Core inbox primitives |
| Messaging | `Vulthil.Messaging.Inbox.Relational` | Relational idempotency store (transactional exactly-once) |
| Messaging | `Vulthil.Messaging.Inbox.Cosmos` | Cosmos idempotency store (effectively-once) |
| Testing | `Vulthil.xUnit` | Base test classes, auto-mocking, and Testcontainers integration |
| Testing | `Vulthil.xUnit.Cosmos` | Cosmos DB emulator fixture for `Vulthil.xUnit` |
| Testing | `Vulthil.Messaging.TestHarness` | In-memory messaging test harness |
| Testing | `Vulthil.Extensions.Testing` | Framework-agnostic polling and HTTP response helpers (no xUnit dependency) |

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

// Infrastructure layer – EF Core context with outbox.
// AddDbContext is an extension on IHostApplicationBuilder; the provider
// extension (UseNpgsql) registers the EF Core context itself.
builder.AddDbContext<AppDbContext>(config => config
    .UseNpgsql("Default")
    .EnableOutboxProcessing());

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
