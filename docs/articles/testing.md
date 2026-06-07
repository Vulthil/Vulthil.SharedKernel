# Testing

`Vulthil.xUnit` and the companion testing packages provide reusable base classes and infrastructure for unit and integration tests.

## Packages

| Package | Purpose |
|---|---|
| `Vulthil.xUnit` | Base test classes, auto-mocking, `WebApplicationFactory` support, and Testcontainers integration |
| `Vulthil.Messaging.TestHarness` | In-memory messaging transport for asserting published/consumed messages |
| `Vulthil.Extensions.Testing` | Shared assertion helpers and test composition utilities |

## Unit Tests

### BaseUnitTestCase

`BaseUnitTestCase` provides an `AutoMocker` instance and a `CancellationToken` scoped to the test:

```csharp
public sealed class CreateUserCommandHandlerTests : BaseUnitTestCase
{
    private readonly Lazy<CreateUserCommandHandler> Target;

    public CreateUserCommandHandlerTests()
    {
        Target = new(() => CreateInstance<CreateUserCommandHandler>());
    }

    [Fact]
    public async Task HandleAsync_CreatesUser()
    {
        // Arrange
        var command = new CreateUserCommand("user@example.com");

        // Act
        var result = await Target.Value.HandleAsync(command, CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
```

### BaseUnitTestCase&lt;TTarget&gt;

When the system-under-test type is accessible, use the generic variant which lazily creates the target for you. The `Target` property unwraps the underlying `Lazy<TTarget>` so you access it directly:

```csharp
public sealed class OrderServiceTests : BaseUnitTestCase<OrderService>
{
    [Fact]
    public async Task PlaceOrder_ReturnsSuccess()
    {
        var result = await Target.PlaceOrderAsync(new PlaceOrderRequest(), CancellationToken);
        Assert.True(result.IsSuccess);
    }
}
```

### Mocking Dependencies

```csharp
// Retrieve a mock
var repoMock = GetMock<IUserRepository>();
repoMock.Setup(r => r.GetByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(user);

// Provide an explicit instance
Use<IOptions<AppSettings>>(Options.Create(new AppSettings { MaxRetries = 3 }));
```

## Integration Tests

### BaseIntegrationTestCase

`BaseIntegrationTestCase<TFactory, TEntryPoint>` boots a real `WebApplicationFactory` backed by test containers. The factory is supplied as the xUnit fixture, so its containers start once for the scope and are shared across the tests in it:

```csharp
public sealed class UsersEndpointTests(AppWebFactory factory)
    : BaseIntegrationTestCase<AppWebFactory, Program>(factory), IClassFixture<AppWebFactory>
{
    [Fact]
    public async Task CreateUser_Returns201()
    {
        var response = await Client.PostAsJsonAsync("/users", new { Email = "a@b.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

Use `IClassFixture<AppWebFactory>` to give each test class its own containers, or a collection fixture (`[Collection]` + `ICollectionFixture<AppWebFactory>`) to share one set of containers across several classes. Different classes/collections run against different containers in parallel, while tests within a scope share containers and reset state between runs.

Key features:

- **Scoped services** – `ScopedServices` gives you a fresh DI scope per test.
- **Automatic database reset** – the database is reset with Respawn after each test, so tests sharing a factory start from a clean state.
- **Log capture** – pass `ITestOutputHelper` to route application logs to the test output.

### Test Containers

`Vulthil.xUnit` ships fixture base classes (in the `Vulthil.xUnit.Fixtures` namespace) that wrap [Testcontainers](https://testcontainers.com/) containers so you can spin up databases, message brokers, and other dependencies as Docker containers. There are three levels, depending on what the container needs to expose:

- `TestContainerFixture<TBuilderEntity, TContainerEntity>` – a plain container with a managed lifecycle (`ITestContainer`).
- `TestContainerFixtureWithConnectionString<TBuilderEntity, TContainerEntity>` – adds a connection string that is injected into the host's configuration under `ConnectionStrings:{ConnectionStringKey}` (`ITestContainerWithConnectionString`). Give `ConnectionStringKey` the bare name (e.g. `"AppDb"`); the factory adds the `ConnectionStrings:` prefix.
- `TestDatabaseContainerFixture<TDbContext, TBuilderEntity, TContainerEntity>` – adds EF Core migrations and Respawn-based data reset between tests (`ITestDatabaseContainer`).

A database fixture overrides `Configure()` to build the container and supplies the Respawn `DbAdapter`, the ADO.NET `DbProviderFactory`, and the configuration key its connection string is bound to:

```csharp
internal sealed class PostgresTestContainer(IMessageSink messageSink)
    : TestDatabaseContainerFixture<AppDbContext, PostgreSqlBuilder, PostgreSqlContainer>(messageSink)
{
    private readonly PostgreSqlBuilder _builder = new PostgreSqlBuilder("postgres:18.1")
        .WithPassword("app");

    protected override PostgreSqlBuilder Configure() => _builder;

    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.Postgres;
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;
    public override string ConnectionStringKey => "AppDb";
}
```

A non-database dependency (for example a message broker) uses `TestContainerFixtureWithConnectionString` and provides its own connection string:

```csharp
public sealed class RabbitMqTestContainer(IMessageSink messageSink)
    : TestContainerFixtureWithConnectionString<RabbitMqBuilder, RabbitMqContainer>(messageSink)
{
    private readonly RabbitMqBuilder _builder = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("guest")
        .WithPassword("guest");

    protected override RabbitMqBuilder Configure() => _builder;

    public override string ConnectionStringKey => "RabbitMq";
    public override string ConnectionString => Container.GetConnectionString();
}
```

Containers are registered on the factory with `AddContainer` (see below), which starts each one once per factory and shares it across the tests in that fixture's scope. Database containers are migrated during host startup and reset with Respawn between tests.

### WebApplicationFactory

`BaseWebApplicationFactory<TEntryPoint>` owns the test containers and serves as the xUnit fixture, so a single derived class replaces the separate factory + fixture pair. Register containers with `AddContainer` (in the constructor or by overriding `ConfigureContainers`); their connection strings are injected into the host and EF Core migrations are ensured during host startup:

```csharp
public sealed class AppWebFactory : BaseWebApplicationFactory<Program>
{
    public AppWebFactory(IMessageSink messageSink)
    {
        AddContainer(new PostgresTestContainer(messageSink));
        AddContainer(new RabbitMqTestContainer(messageSink));
    }

    // ConfigureWebHost is sealed; override ConfigureCustomWebHost for extra host setup.
    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with test doubles
        });
    }
}
```

Migrations run from a startup initializer placed at the front of the host's hosted-service list, so the schema exists **before the application's own background services start** (e.g. an outbox processor that polls the database immediately). It applies only migrations that are still pending and tolerates a concurrent migrator, so an application that already migrates itself on startup (for example `app.MigrateAsync()` in `Program.cs`) keeps ownership — the factory sees the schema is up to date and does nothing. Apps that don't self-migrate get migrated by the factory automatically. No test-only environment or production-code changes are required.

### Mocking outbound HTTP dependencies

For a service that calls an external API through an `HttpClient` from `IHttpClientFactory`, register an in-process HTTP mock on the factory. It replaces that client's primary message handler, so the real client code runs (URL building, serialization, the delegating-handler pipeline) and only the wire is faked. Both **typed** clients (`AddHttpClient<TClient, ...>()`) and **named** clients (`AddHttpClient("name")`) are supported; for typed clients the implementation type does not need to be accessible:

```csharp
public sealed class AppWebFactory : BaseWebApplicationFactory<Program>
{
    public AppWebFactory(IMessageSink messageSink)
    {
        AddContainer(new PostgresTestContainer(messageSink));
        AddHttpMock<IWeatherClient>();   // typed:  AddHttpClient<IWeatherClient, WeatherClient>()
        AddHttpMock("inventory");        // named:  AddHttpClient("inventory")
    }
}
```

Retrieve the named mock the same way — `HttpMock("inventory")` (or `GetHttpMock("inventory")` on the factory) — and configure it exactly like the typed one.

Configure responses per test via `HttpMock<TClient>()` and inspect what was sent via `ReceivedRequests`:

```csharp
[Fact]
public async Task Uses_external_forecast()
{
    // Strongly-typed body, serialized to JSON, plus a response header:
    HttpMock<IWeatherClient>()
        .On(HttpMethod.Get, "/forecast/london")
        .RespondWith(HttpStatusCode.OK, new Forecast("London", 18))
        .WithHeader("X-Source", "mock");

    // Or replay a real response captured from the live endpoint and saved as a JSON document:
    HttpMock<IWeatherClient>()
        .On(HttpMethod.Get, "/forecast/paris")
        .RespondWithJson(HttpStatusCode.OK, await File.ReadAllTextAsync("captured/paris.json"));

    var result = await Client.GetAsync("/weather/london");

    HttpMock<IWeatherClient>().ReceivedRequests
        .ShouldContain(r => r.RequestUri!.AbsolutePath == "/forecast/london");
}
```

Mock state is reset after each test (like the database), so stubs and captured requests never leak between tests. Under the hood the mock implements `IResettableResource`; database containers implement it too, and the test case resets every registered resettable resource in its teardown. A WireMock-based or other `IHttpMock` implementation can be substituted if you need richer matching, but the built-in mock has no external dependency.

## Messaging Test Harness

`Vulthil.Messaging.TestHarness` provides an in-memory transport that runs your consumers with no broker and
captures every produced and consumed message for assertion. It is built entirely on the public
`Vulthil.Messaging.Transport` SDK, so it mirrors the real consumer topology assembled from your queue
configuration. Dispatch is synchronous — by the time a publish/send/request call returns, every consumer (and
stub) it triggered has run, so assertions need no polling.

### Composing a harness (unit/component tests)

Call `UseTestHarness()` in place of a broker transport, then resolve `ITestHarness` alongside the usual
`IPublisher`/`ISendEndpoint`/`IRequester`:

```csharp
var builder = Host.CreateApplicationBuilder();
builder.AddMessaging(messaging =>
{
    messaging.ConfigureQueue("orders", q => q.AddConsumer<OrderCreatedConsumer>());
    messaging.UseTestHarness();
});
using var host = builder.Build();

var publisher = host.Services.GetRequiredService<IPublisher>();
var harness = host.Services.GetRequiredService<ITestHarness>();

await publisher.PublishAsync(new OrderCreatedEvent(orderId));

harness.Published<OrderCreatedEvent>().ShouldHaveSingleItem().Message.OrderId.ShouldBe(orderId);
harness.Consumed<OrderCreatedEvent>().ShouldHaveSingleItem();
```

`ITestHarness` exposes `Published<T>()`, `Sent<T>()`, `Consumed<T>()`, and `Requested<T>()` (each returns the
matching `CapturedMessage<T>` items — `.Message` is the payload, `.Envelope` the wire metadata), plus `Clear()`.

### Mocking responses

A test can stand in for an external service. `Respond<TRequest, TResponse>` answers a request (taking precedence
over a real request consumer), and `Handle<TMessage>` reacts to a published or sent message — useful to fake a
downstream service that publishes a follow-up:

```csharp
harness.Respond<GetWeatherRequest, WeatherForecast>(ctx => new WeatherForecast(ctx.Message.City, 20));
harness.Handle<OrderShippedEvent>(ctx => { observed.Add(ctx.Message.OrderId); return Task.CompletedTask; });

var result = await requester.RequestAsync<GetWeatherRequest, WeatherForecast>(new GetWeatherRequest("Oslo"));
result.Value.TemperatureC.ShouldBe(20);
```

A request with neither a responder nor a registered request consumer completes with a
`Messaging.Request.NoConsumer` failure; a request consumer that throws surfaces as a `Messaging.Request.Failure`.

### Swapping the transport in integration tests

To exercise the production composition root without a broker, call `ReplaceTransportWithTestHarness()` from the
test host's service hook (for example a `WebApplicationFactory`). It swaps the registered transport for the
harness and leaves the rest of the application untouched — production code is not modified for tests:

```csharp
public sealed class AppWebFactory : BaseWebApplicationFactory<Program>
{
    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
        => builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
}
```

The orphaned broker registrations remain but are never resolved, so no connection is attempted. Disable the
broker's own health check via configuration (for example `Aspire:RabbitMQ:Client:DisableHealthChecks`) if a
readiness probe would otherwise wait on it.

See [Messaging](messaging.md) for more on the messaging architecture.
