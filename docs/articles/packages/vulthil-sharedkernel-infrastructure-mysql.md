# Vulthil.SharedKernel.Infrastructure.MySql

Use `Vulthil.SharedKernel.Infrastructure.MySql` to select the MySQL-tuned outbox strategy when your `DbContext` runs against MySQL.

## When to use

- MySQL is the underlying database for an application's primary `DbContext`
- Outbox processing should use the MySQL strategy

## Pattern

- Call `UseMySql()` on the database infrastructure configurator to swap in the MySQL outbox strategy
- Register the `DbContext` itself with your preferred MySQL EF Core integration (e.g. `Pomelo.EntityFrameworkCore.MySql`) – `UseMySql` only switches the outbox strategy and does not register the EF Core provider
- Apply migrations on startup with `MigrateAsync<TDbContext>()` from `Vulthil.SharedKernel.Infrastructure.Relational` (pulled in transitively)

## Usage

### Registration

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("Default"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Default"))));

builder.AddDbContext<AppDbContext>(config => config
    .UseMySql()
    .EnableOutboxProcessing(o =>
    {
        o.BatchSize = 25;
        o.MaxRetries = 3;
    }));
```

### Applying migrations on startup

```csharp
var app = builder.Build();
await app.MigrateAsync<AppDbContext>();
app.Run();
```
