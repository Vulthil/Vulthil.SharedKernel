# Testing

`Vulthil.xUnit` and the companion testing packages provide reusable base classes and infrastructure for unit and integration tests.

## Packages

| Package | Purpose |
|---|---|
| `Vulthil.xUnit` | Base test classes, auto-mocking, `WebApplicationFactory` support, and Testcontainers integration |
| `Vulthil.xUnit.Cosmos` | Azure Cosmos DB emulator fixture with a database per test class |
| `Vulthil.Messaging.TestHarness` | In-memory messaging transport for asserting published/consumed messages |
| `Vulthil.Extensions.Testing` | Framework-agnostic helpers — `Result`-based polling, HTTP response deserialization and the `IResettableTestState` contract; no xUnit dependency |

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

// Use a real implementation instead of an auto-generated mock, with its own dependencies still auto-mocked
UseReal<OrderPricingCalculator>();
UseRealFor<IOrderPricingCalculator, OrderPricingCalculator>();
```

`UseReal`/`UseRealFor` register the real type lazily — it is constructed the first time it is resolved (directly, or
as another created instance's dependency), so a dependency registered afterward but before that first resolution is
still picked up. Every disposable instance the auto-mocker holds — an explicit `Use()` instance, a `UseReal`/
`UseRealFor` instance once resolved, or an auto-generated dependency mock — is disposed automatically after each
test; only a synchronous `IDisposable` is covered, so an `IAsyncDisposable`-only registration needs an override of
`Dispose()` to dispose it explicitly. The `BaseUnitTestCase<TTarget>` variant's `Target` is disposed the same way,
and always before the auto-mocker's own instances.

## Integration Tests

### BaseIntegrationTestCase

`BaseIntegrationTestCase<TEntryPoint>` boots a real `WebApplicationFactory` backed by test containers. It builds on
`BaseUnitTestCase`, so the cancellation token, the `Initialize()`/`Dispose()` hooks and the auto-mocker vocabulary
(`Use`, `GetMock`, `UseReal`) are the same in both kinds of test. The factory is supplied as the xUnit fixture, so its
containers start once for the scope and are shared across the tests in it:

```csharp
public sealed class UsersEndpointTests(AppWebFactory factory)
    : BaseIntegrationTestCase<Program>(factory), IClassFixture<AppWebFactory>
{
    [Fact]
    public async Task CreateUser_Returns201()
    {
        var response = await Client.PostAsJsonAsync("/users", new { Email = "a@b.com" }, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

Use `IClassFixture<AppWebFactory>` so each test class gets its own factory and, through the factory's
[`ContainerHost`](#sharing-containers-across-the-assembly-containerhost), its own isolated scope inside the shared
containers. Tests within a class share the factory's single test host and reset state between runs.

Key features:

- **Scoped services** – `ScopedServices` gives you a fresh DI scope per test.
- **Mocking an application service** – register a double on the test case before the first use of `Factory`,
  `Client` or `ScopedServices` — `GetMock<IWeatherClient>()`, `Use<IWeatherClient>(stub)` or `UseReal<...>()` — and
  the test runs on a per-test copy of the host with that service replaced. Register under the type the application
  resolves (`Use<IWeatherClient>(stub)`, not `Use(stub)`). A test that registers nothing runs on the shared host;
  registering after the host is built throws, so a test never silently runs against the real service. Outbound HTTP
  has its own mock, see [below](#mocking-outbound-http-dependencies). The per-test host uses the class's databases and
  virtual hosts, so while it runs, the shared host pauses its restartable hosted services (see the reset below) and
  resumes them after the test: the two hosts never compete for the same queue or the same database rows.
- **Automatic database reset** – the database is reset with Respawn after each test, so tests sharing a factory
  start from a clean state. Hosted services implementing `IRestartableHostedService` (from
  `Vulthil.Extensions.Hosting`) on every live host of the class are stopped around the reset and restarted afterwards,
  so a database-polling relay such as the outbox background service never contends with it, and the message consumers
  stop consuming until the reset is done. Only the newest live host runs those services at any time: a per-test host
  pauses the hosts built before it until it stops. Every stop, reset and restart step is bounded by its own
  30-second timeout rather than the test's cancellation token, a failing step never skips the remaining ones, and all
  failures are reported together — a test that timed out still leaves a clean fixture for the next one.
- **Test double reset** – a test double that keeps state in the host's services, such as the
  [messaging test harness](#messaging-test-harness), is reset after each test in the same pause when it implements
  `IResettableTestState`. See [Resetting your own test doubles](#resetting-your-own-test-doubles).
- **Log capture** – application logs are routed to the currently running test automatically (via `TestContext`). The
  `ITestOutputHelper` constructor parameter is for writing test output directly.
- **One host per class** – all tests in a class run against the fixture's test host. Override `CreateFactory()`
  (e.g. `FactoryFixture.WithWebHostBuilder(...)`) when a class needs other per-test host configuration; call
  `ConfigureTestCaseServices` from that builder's `ConfigureTestServices` to keep the registered doubles. Derived
  factories are disposed after each test.

### Test Containers

`Vulthil.xUnit` ships fixture base classes (in the `Vulthil.xUnit.Fixtures` namespace) that wrap [Testcontainers](https://testcontainers.com/) containers so you can spin up databases, message brokers, and other dependencies as Docker containers. There are three levels, depending on what the container needs to expose:

- `TestContainerFixture<TBuilderEntity, TContainerEntity>` – a plain container with a managed lifecycle (`ITestContainer`).
- `TestContainerFixtureWithConnectionString<TBuilderEntity, TContainerEntity>` – adds a connection string that is injected into the host's configuration under `ConnectionStrings:{ConnectionStringKey}` (`ITestContainerWithConnectionString`). Give `ConnectionStringKey` the bare name (e.g. `"AppDb"`); the factory adds the `ConnectionStrings:` prefix. Every container a factory consumes needs its own key: when two consumed containers use the same key (compared case-insensitively, like configuration keys), the factory's `InitializeAsync` throws instead of letting one connection string overwrite the other.
- `TestDatabaseContainerFixture<TDbContext, TBuilderEntity, TContainerEntity>` – adds EF Core migrations and Respawn-based data reset between tests (`ITestDatabaseContainer`).

None of them needs constructor arguments. Pass an `IMessageSink` to route Testcontainers' own log output somewhere
specific; without one it is forwarded to xUnit's diagnostic messages (visible with `diagnosticMessages` enabled).

A database fixture overrides `Configure()` to build the container and supplies the Respawn `DbAdapter`, the ADO.NET `DbProviderFactory`, and the configuration key its connection string is bound to:

```csharp
internal sealed class PostgresTestContainer
    : TestDatabaseContainerFixture<AppDbContext, PostgreSqlBuilder, PostgreSqlContainer>
{
    private readonly PostgreSqlBuilder _builder = new PostgreSqlBuilder("postgres:18.1")
        .WithPassword("app");

    protected override PostgreSqlBuilder Configure() => _builder;

    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.Postgres;
    public override DbProviderFactory DbProviderFactory => NpgsqlFactory.Instance;
    public override string ConnectionStringKey => "AppDb";
}
```

A message broker uses `RabbitMqTestContainerFixture` (which adds virtual-host-per-scope isolation); any other non-database dependency uses `TestContainerFixtureWithConnectionString` directly. Both just provide the container configuration and connection string:

```csharp
public sealed class RabbitMqTestContainer
    : RabbitMqTestContainerFixture<RabbitMqBuilder, RabbitMqContainer>
{
    private readonly RabbitMqBuilder _builder = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername("guest")
        .WithPassword("guest");

    protected override RabbitMqBuilder Configure() => _builder;

    public override string ConnectionStringKey => "RabbitMq";
    public override string ConnectionString => Container.GetConnectionString();
}
```

Containers are registered once on a [`ContainerHost`](#sharing-containers-across-the-assembly-containerhost) and consumed by every factory in the assembly through an isolated per-class scope. Database containers are migrated during host startup and reset with Respawn between tests.

### WebApplicationFactory

`BaseWebApplicationFactory<TEntryPoint>` consumes the containers of a `ContainerHost` and serves as the xUnit fixture, so a single derived class acts as both the factory and the fixture. It injects the connection strings of the containers it consumes into the host and ensures EF Core migrations run during host startup:

```csharp
public sealed class AppWebFactory(AppContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    // ConfigureWebHost is sealed; override ConfigureCustomWebHost for extra host setup.
    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with test doubles for every test in the class
        });
    }
}
```

Migrations run from a startup initializer placed at the front of the host's hosted-service list, so the schema exists **before the application's own background services start** (e.g. an outbox processor that polls the database immediately). It applies only migrations that are still pending and tolerates a concurrent migrator, so an application that already migrates itself on startup (for example `app.MigrateAsync()` in `Program.cs`) keeps ownership — the factory sees the schema is up to date and does nothing. Apps that don't self-migrate get migrated by the factory automatically. No test-only environment or production-code changes are required.

### Sharing containers across the assembly (ContainerHost)

Containers are registered **once**, on an assembly-level `ContainerHost` fixture, and every factory consumes them through a per-factory **scope** — so twenty test classes share one PostgreSQL server and one broker instead of starting twenty of each:

```csharp
public sealed class AppContainerHost : ContainerHost
{
    protected override Task ConfigureContainers()
    {
        AddContainer<PostgresTestContainer>();
        AddContainer<RabbitMqTestContainer>();
        return Task.CompletedTask;
    }
}

[assembly: AssemblyFixture<AppContainerHost>]

// The factory consumes every container registered on the host.
public sealed class AppWebFactory(AppContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost);
```

Every container on the host is consumed automatically, so containers are managed in one place. Each factory instance (one per test class with `IClassFixture`) gets its own **scope** inside the shared containers, so parallel test classes never see each other's state. Scoping is built into the fixture base classes; a consumer only derives thin container wrappers:

- `TestDatabaseContainerFixture` creates a uniquely named database per scope on the shared server, migrates it during host startup, resets it with Respawn between tests, and drops it (best-effort) when the class finishes. Database DDL is engine-aware through the fixture's `DbAdapter` (PostgreSQL drops use `WITH (FORCE)`, SQL Server switches to single-user); `BuildScopedConnectionString`, `CreateDatabaseAsync`, and `DropDatabaseAsync` are overridable for exotic engines.
- `RabbitMqTestContainerFixture` creates a **virtual host** per scope on the shared broker (via `rabbitmqctl` inside the container), so parallel classes never see each other's exchanges, queues, or messages.
- `CosmosTestContainerFixture` (in the `Vulthil.xUnit.Cosmos` package) starts one Cosmos emulator and gives each scope its own **emulator database**, recreated between tests. It provisions and resets each database through your `DbContext` resolved from the test host's DI container — so a context whose constructor takes more than its options just works — while a bare `DbContext` is used only to probe the emulator for readiness and to drop a scope's database on teardown.
- Any other `TestContainerFixtureWithConnectionString` returns a pass-through scope by default — consumers share the container's namespace; override `CreateScope` only when the service offers some other isolation unit (see below).
- Containers start **lazily** on first use: a filtered run only pays for the containers its factories actually consume, and concurrent factories share one startup task per container.
- A factory that should not consume every host container overrides `ShouldUseContainer` (e.g. a factory that swaps the broker for the in-memory test harness consumes only the database container).

The scope identifier defaults to the factory type name plus a random suffix (override `CreateScopeId()` to change it), so two classes using the same factory type still get distinct databases and virtual hosts.

To give your own container per-scope isolation, return a view from `CreateScope` that derives from `TestContainerWithConnectionStringScope<TContainer>` (or `TestContainerScope<TContainer>` for a container without a connection string). The base view forwards the host configuration and the connection string key to the shared container, so the view only describes its namespace:

```csharp
public sealed class SearchTestContainer : TestContainerFixtureWithConnectionString<SearchBuilder, SearchContainer>
{
    public override string ConnectionString => Container.GetConnectionString();
    public override string ConnectionStringKey => "search";

    public override ITestContainer CreateScope(string scopeId) => new IndexScope(this, scopeId);

    private sealed class IndexScope(SearchTestContainer container, string index)
        : TestContainerWithConnectionStringScope<SearchTestContainer>(container, index)
    {
        private readonly string _index = index;

        // Point the scope's consumers at their own index.
        public override string ConnectionString => $"{Container.ConnectionString};DefaultIndex={_index}";

        protected override ValueTask CreateNamespaceAsync(string namespaceName) => Container.CreateIndexAsync(namespaceName);

        protected override ValueTask DeleteNamespaceAsync(string namespaceName) => Container.DeleteIndexAsync(namespaceName);
    }
}
```

The view creates the namespace when the factory starts and deletes it when the class finishes. Deleting is best-effort: a failure becomes a diagnostic message instead of a test failure, because the namespace goes away with the container anyway. The base view never resets the shared container; implement `IResettableResource` on the view when its namespace can be reset between tests.

A scope lives as long as its factory: one test class. All tests of the class — and the per-test hosts they build — share its database and virtual host. The database is reset after each test, but the queues are not purged, so a message that one test leaves in a queue is delivered during the next test of the class. Wait for the messages a test publishes before the test ends.

### Mocking outbound HTTP dependencies

For a service that calls an external API through an `HttpClient` from `IHttpClientFactory`, register an in-process HTTP mock on the factory. It replaces that client's primary message handler, so the real client code runs (URL building, serialization, the delegating-handler pipeline) and only the wire is faked. Both **typed** clients (`AddHttpClient<TClient, ...>()`, generic client types included) and **named** clients (`AddHttpClient("name")`) are supported; for typed clients the implementation type does not need to be accessible:

```csharp
public sealed class AppWebFactory : BaseWebApplicationFactory<Program>
{
    public AppWebFactory(AppContainerHost containerHost) : base(containerHost)
    {
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

> [!IMPORTANT]
> When several rules match a request, the rule registered **first** wins. Moq and NSubstitute work the other way
> around: there the last setup wins. Register specific rules before general ones.

`ReceivedRequests` keeps the method, the URI, the headers and the body of each request. Header names are
case-insensitive:

```csharp
var request = HttpMock<IWeatherClient>().ReceivedRequests.ShouldHaveSingleItem();
request.Headers["Authorization"].ShouldHaveSingleItem().ShouldBe("Bearer test-token");
```

Mock state is reset after each test (like the database), so stubs and captured requests never leak between tests. Under the hood the mock implements `IResettableResource`; database containers implement it too, and the test case resets every registered resettable resource in its teardown. A WireMock-based or other `IHttpMock` implementation can be substituted if you need richer matching, but the built-in mock has no external dependency.

### Resetting your own test doubles

A test double that keeps state in the test host's services — an in-memory email sender that records what it sent,
a stub store — is reset after each test when it implements `IResettableTestState` (from `Vulthil.Extensions.Testing`)
and is registered as one:

```csharp
public sealed class InMemoryEmailSender : IEmailSender, IResettableTestState
{
    private readonly ConcurrentQueue<Email> _sent = new();

    public IReadOnlyCollection<Email> Sent => _sent;

    public Task SendAsync(Email email, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(email);
        return Task.CompletedTask;
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        _sent.Clear();
        return ValueTask.CompletedTask;
    }
}

public sealed class AppWebFactory(AppContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    protected override void ConfigureCustomWebHost(IWebHostBuilder builder) => builder.ConfigureTestServices(services =>
    {
        services.AddSingleton<InMemoryEmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<InMemoryEmailSender>());
        services.AddSingleton<IResettableTestState>(sp => sp.GetRequiredService<InMemoryEmailSender>());
    });
}
```

After each test, the test case resets every `IResettableTestState` of every live host of the class. The reset runs
in the same pause as the database reset and has the same 30-second bound; the token passed to `ResetAsync` is
cancelled when the bound runs out. The reset runs after each test, not before it: what the host records while it
starts, or while its background services restart after a reset, is visible to the test that runs next.

## Messaging Test Harness

`Vulthil.Messaging.TestHarness` provides an in-memory transport that runs your consumers with no broker and
captures every produced and consumed message for assertion. It is built entirely on the public
`Vulthil.Messaging.Transport` SDK, so it assembles the same execution plans — consumers, polymorphic dispatch,
per-consumer retry resolution — from your queue configuration that a real transport would. Dispatch is
synchronous — by the time a publish/send/request call returns, every consumer (and stub) it triggered has run,
so assertions need no polling.

Two fidelity limits to keep in mind: the harness dispatches each produced message **once** to all matching
consumers (a real broker delivers a distinct copy per subscribed queue), and partition lanes are not simulated
(dispatch is inline and ordered by call).

The harness runs each delivery through the core `DeliveryDispatcher`, the same delivery rules the RabbitMQ transport
and any custom transport use (see [Writing a Custom Transport](messaging.md#writing-a-custom-transport)):

- Consumers of one message retry in rounds: each round re-runs only the consumers that failed, so a consumer that
  completed never runs twice. Retries run back-to-back, without the configured delays.
- Every attempt gets its own DI scope.
- A one-way consumer that has failed for good publishes a `Fault<T>`, while the publish call itself completes
  normally. The fault is observable via `Published<Fault<TMessage>>()` and runs any `Handle<Fault<TMessage>>`
  stub, but, as on the broker, it never reaches a registered `IConsumer<Fault<TMessage>>`.
- When the caller's cancellation token ends a delivery, a publish or send throws `OperationCanceledException` and
  a request returns a `Messaging.Request.Cancelled` failure. A consumer's own `OperationCanceledException` is an
  ordinary failure and is retried.

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

### Resetting between tests

`ITestHarness` is registered as a singleton, so one host keeps one harness for every test it serves. The harness is
also registered as an `IResettableTestState`, so `BaseIntegrationTestCase` resets it after each test: it clears the
captured messages and removes the `Handle`/`Respond` stubs (see
[Resetting your own test doubles](#resetting-your-own-test-doubles)). A test needs no setup code for this.

The reset runs after each test, so messages that the host publishes while it starts are visible to the first test
that uses the host. To ignore them, assert on the ids the test used, or call `Clear()` in the test body after the
test registers its doubles. Do not call it from `Initialize()`: resolving the harness there builds the host before
the test can register its doubles.

`Clear()` empties only the captured messages and keeps the stubs, for a test that asserts in phases. A host that you
build yourself and keep across tests is not reset for you; reset it from your per-test teardown:

```csharp
foreach (var testState in host.Services.GetServices<IResettableTestState>())
{
    await testState.ResetAsync(cancellationToken);
}
```

A harness resolved from a fresh host per test (a new `Host.CreateApplicationBuilder().Build()` in the constructor,
disposed in teardown, as in the snippet above) needs no reset, since each test gets its own instance.

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

A request with neither a responder nor a registered request consumer **times out** with a
`Messaging.Request.Timeout` failure — just as it would against a real broker, where no consumer means no reply;
a request consumer that throws surfaces as a `Messaging.Request.Failure`.

### Swapping the transport in integration tests

To exercise the production composition root without a broker, call `ReplaceTransportWithTestHarness()` from the
test host's service hook (for example a `WebApplicationFactory`). It swaps the registered transport for the
harness and leaves the rest of the application untouched — production code is not modified for tests:

```csharp
public sealed class AppWebFactory(AppContainerHost containerHost) : BaseWebApplicationFactory<Program>(containerHost)
{
    protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
        => builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
}
```

The orphaned broker registrations remain but are never resolved, so no connection is attempted. Disable the
broker's own health check via configuration (for example `Aspire:RabbitMQ:Client:DisableHealthChecks`) if a
readiness probe would otherwise wait on it.

See [Messaging](messaging.md) for more on the messaging architecture.
