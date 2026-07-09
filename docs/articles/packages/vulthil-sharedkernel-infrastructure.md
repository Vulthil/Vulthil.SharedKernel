# Vulthil.SharedKernel.Infrastructure

Use `Vulthil.SharedKernel.Infrastructure` for persistence and outbox integration. It hosts the outbox engine
(factored into [`Vulthil.SharedKernel.Outbox`](vulthil-sharedkernel-outbox.md)) and adds the `DbContext` base and
the DI wiring (`EnableOutboxProcessing`).

## When to use

- EF Core `DbContext` setup and migrations/ensure-created helpers
- Transaction wrappers and repository implementations
- Outbox persistence and background processing

## Pattern

- Keep persistence mapping in infrastructure only
- Publish domain events through outbox for reliability
- Register infrastructure via composition-root extension methods

## Usage

### Defining a DbContext

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : BaseDbContext(options)
{
    protected override Assembly? ConfigurationAssembly => typeof(AppDbContext).Assembly;

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyNpgsqlOutbox();
    }
}
```

### Registration with outbox processing

`AddDbContext` is an extension on `IHostApplicationBuilder`. The provider extension (e.g. `UseNpgsql`, in `Vulthil.SharedKernel.Infrastructure.Npgsql`) handles the EF Core registration, so chain it directly on the configurator:

```csharp
builder.AddDbContext<AppDbContext>(config => config
    .UseNpgsql("Default")
    .EnableOutboxProcessing(o =>
    {
        o.BatchSize = 10;
        o.MaxRetries = 3;
    }));
```

Only one outbox-enabled `DbContext` is supported per host: the relay and retention background services resolve a
single `IOutboxStore`, so a second `EnableOutboxProcessing()` call (on a different `DbContext`) throws an
`InvalidOperationException` at startup instead of silently leaving the first context's messages unrelayed.

### Generic repository

```csharp
public sealed class UserRepository(AppDbContext db)
    : GenericRepository<AppDbContext, User, UserId>(db)
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        DbContext.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
}
```

### Database initialization

```csharp
// Apply pending migrations on startup
await app.MigrateAsync<AppDbContext>();

// Or ensure created (for development/testing)
await app.EnsureCreatedAsync<AppDbContext>();
```
