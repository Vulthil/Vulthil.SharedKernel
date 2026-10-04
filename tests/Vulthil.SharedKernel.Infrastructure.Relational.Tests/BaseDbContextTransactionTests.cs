using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Infrastructure.Relational.Tests;

/// <summary>
/// Pins what <see cref="BaseDbContext"/>'s <c>ExecuteInTransactionAsync</c> does with the change tracker, on SQLite
/// under a strategy that retries once on a <see cref="TimeoutException"/>: the first attempt keeps what the context
/// tracked before the call, a retry starts from a clean change tracker, and a retry that would drop unsaved changes
/// from before the call throws instead.
/// </summary>
public sealed class BaseDbContextTransactionTests : BaseUnitTestCase
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Lazy<OrdersDbContext> _lazyTarget;

    private OrdersDbContext Target => _lazyTarget.Value;

    public BaseDbContextTransactionTests()
    {
        _lazyTarget = new(CreateInstance<OrdersDbContext>);
        Use(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite(_connection, sqlite => sqlite.ExecutionStrategy(dependencies => new RetryOnceExecutionStrategy(dependencies)))
            .Options);
    }

    protected override async ValueTask Initialize()
    {
        await _connection.OpenAsync(CancellationToken);
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync(CancellationToken);
    }

    protected override async ValueTask Dispose()
    {
        if (_lazyTarget.IsValueCreated)
        {
            await Target.DisposeAsync();
        }

        await _connection.DisposeAsync();
        await base.Dispose();
    }

    [Fact]
    public async Task AnOrderAddedBeforeTheCallIsSavedInTheTransaction()
    {
        // Arrange
        Target.Orders.Add(new Order { Name = "added before" });

        // Act
        await Target.ExecuteInTransactionAsync(cancellationToken => Target.SaveChangesAsync(cancellationToken), CancellationToken);

        // Assert
        (await OrderNamesAsync()).ShouldBe(["added before"]);
    }

    [Fact]
    public async Task AnOrderLoadedBeforeTheCallAndRenamedInsideIsSaved()
    {
        // Arrange
        await SeedOrderAsync("draft");
        var order = await Target.Orders.SingleAsync(CancellationToken);

        // Act
        await Target.ExecuteInTransactionAsync(cancellationToken =>
        {
            order.Name = "shipped";
            return Target.SaveChangesAsync(cancellationToken);
        }, CancellationToken);

        // Assert
        (await OrderNamesAsync()).ShouldBe(["shipped"]);
    }

    [Fact]
    public async Task ARetryRunsTheOperationAgainFromACleanChangeTracker()
    {
        // Arrange
        var attempts = 0;

        // Act
        await Target.ExecuteInTransactionAsync(cancellationToken =>
        {
            attempts++;
            Target.Orders.Add(new Order { Name = $"attempt {attempts}" });
            if (attempts == 1)
            {
                throw new TimeoutException("transient");
            }

            return Target.SaveChangesAsync(cancellationToken);
        }, CancellationToken);

        // Assert
        attempts.ShouldBe(2);
        (await OrderNamesAsync()).ShouldBe(["attempt 2"]);
    }

    [Fact]
    public async Task ARetryThrowsInsteadOfDroppingUnsavedChangesFromBeforeTheCall()
    {
        // Arrange
        Target.Orders.Add(new Order { Name = "added before" });
        var attempts = 0;

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Target.ExecuteInTransactionAsync(cancellationToken =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TimeoutException("transient");
            }

            return Target.SaveChangesAsync(cancellationToken);
        }, CancellationToken));

        // Assert
        attempts.ShouldBe(1);
        exception.Message.ShouldContain("held unsaved changes before the call");
        (await OrderNamesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task AnOrderOnlyReadBeforeTheCallDoesNotBlockARetry()
    {
        // Arrange
        await SeedOrderAsync("existing");
        _ = await Target.Orders.SingleAsync(CancellationToken);
        var attempts = 0;

        // Act
        await Target.ExecuteInTransactionAsync(cancellationToken =>
        {
            attempts++;
            Target.Orders.Add(new Order { Name = "new" });
            if (attempts == 1)
            {
                throw new TimeoutException("transient");
            }

            return Target.SaveChangesAsync(cancellationToken);
        }, CancellationToken);

        // Assert
        attempts.ShouldBe(2);
        (await OrderNamesAsync()).ShouldBe(["existing", "new"], ignoreOrder: true);
    }

    [Fact]
    public async Task InsideAnOpenTransactionTheOperationJoinsItAndTheOuterTransactionDecides()
    {
        // Arrange
        await using var context = NewContext();
        await using var outer = await context.Database.BeginTransactionAsync(CancellationToken);
        context.Orders.Add(new Order { Name = "joined" });

        // Act
        await context.ExecuteInTransactionAsync(cancellationToken => context.SaveChangesAsync(cancellationToken), CancellationToken);
        var namesInsideTheOuterTransaction = await context.Orders.AsNoTracking().Select(order => order.Name).ToArrayAsync(CancellationToken);
        await outer.RollbackAsync(CancellationToken);

        // Assert
        namesInsideTheOuterTransaction.ShouldBe(["joined"]);
        (await OrderNamesAsync()).ShouldBeEmpty();
    }

    private OrdersDbContext NewContext() => new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    private async Task SeedOrderAsync(string name)
    {
        await using var context = NewContext();
        context.Orders.Add(new Order { Name = name });
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<string[]> OrderNamesAsync()
    {
        await using var context = NewContext();
        return await context.Orders.Select(order => order.Name).ToArrayAsync(CancellationToken);
    }

    public sealed class Order
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public required string Name { get; set; }
    }

    public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : BaseDbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override Assembly? ConfigurationAssembly => null;
    }

    /// <summary>
    /// Retries once, with no delay, when the operation throws a <see cref="TimeoutException"/>, standing in for a
    /// provider's retrying strategy and its transient faults.
    /// </summary>
    public sealed class RetryOnceExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, maxRetryCount: 1, maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }
}
