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

When the system-under-test type is accessible, use the generic variant which lazily creates the target for you:

```csharp
public sealed class OrderServiceTests : BaseUnitTestCase<OrderService>
{
    [Fact]
    public async Task PlaceOrder_ReturnsSuccess()
    {
        var result = await Target.Value.PlaceOrderAsync(new PlaceOrderRequest(), CancellationToken);
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

`BaseIntegrationTestCase<TFactory, TEntryPoint>` boots a real `WebApplicationFactory` backed by test containers:

```csharp
public sealed class UsersEndpointTests(TestFixture fixture)
    : BaseIntegrationTestCase<AppWebFactory, Program>(fixture)
{
    [Fact]
    public async Task CreateUser_Returns201()
    {
        var response = await Client.PostAsJsonAsync("/users", new { Email = "a@b.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

Key features:

- **Scoped services** – `ScopedServices` gives you a fresh DI scope per test.
- **Automatic database reset** – the fixture calls `ResetDatabase()` after each test using Respawn.
- **Log capture** – pass `ITestOutputHelper` to route application logs to the test output.

### Test Containers

`Vulthil.xUnit` ships container abstractions so you can spin up databases (PostgreSQL, SQL Server, etc.) as Docker containers:

```csharp
public sealed class PostgresContainerPool
    : IDatabaseContainerPool<PostgreSqlContainer>
{
    // Configure container image, ports, credentials, etc.
}
```

Container pools are shared across tests through xUnit fixtures, so the container is started once and reused.

### WebApplicationFactory

Derive from `BaseWebApplicationFactory<TEntryPoint>` to customise the test host:

```csharp
public sealed class AppWebFactory : BaseWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with test doubles
        });
    }
}
```

## Messaging Test Harness

Replace the real transport with the test harness to assert messaging behaviour without a broker:

```csharp
var published = testHarness.Published<OrderCreatedEvent>();
Assert.Single(published);
Assert.Equal(expectedOrderId, published.First().Message.OrderId);
```

See [Messaging](messaging.md) for more on the messaging architecture.
