using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Infrastructure.Relational.Tests;

/// <summary>
/// Pins the host's <see cref="IUnitOfWork"/> when <c>AddDbContext</c> registers one or several contexts: one context is
/// its own unit of work, and several share one unit of work that saves, opens transactions on and commits every
/// context, the outbox context last. Two SQLite databases stand in for two relational contexts, and a strategy that
/// retries once on a <see cref="TimeoutException"/> stands in for a provider's retrying strategy.
/// </summary>
public sealed class UnitOfWorkAcrossContextsTests : BaseUnitTestCase
{
    private const string CosmosConnectionString =
        "AccountEndpoint=https://localhost:1/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly SqliteConnection _ordersConnection = new("DataSource=:memory:");
    private readonly SqliteConnection _billingConnection = new("DataSource=:memory:");
    private readonly ConcurrentQueue<string> _commits = new();
    private readonly HostApplicationBuilder _builder = Host.CreateApplicationBuilder();
    private readonly Lazy<IHost> _lazyHost;
    private AsyncServiceScope? _scope;

    public UnitOfWorkAcrossContextsTests()
    {
        _builder.Logging.ClearProviders();
        _lazyHost = new(_builder.Build);
    }

    private IServiceProvider Services => (_scope ??= _lazyHost.Value.Services.CreateAsyncScope()).ServiceProvider;

    private IUnitOfWork Target => Services.GetRequiredService<IUnitOfWork>();

    private OrdersContext Orders => Services.GetRequiredService<OrdersContext>();

    private BillingContext Billing => Services.GetRequiredService<BillingContext>();

    protected override async ValueTask Initialize()
    {
        await _ordersConnection.OpenAsync(CancellationToken);
        await _billingConnection.OpenAsync(CancellationToken);
    }

    protected override async ValueTask Dispose()
    {
        if (_scope is { } scope)
        {
            await scope.DisposeAsync();
        }

        if (_lazyHost.IsValueCreated)
        {
            _lazyHost.Value.Dispose();
        }

        await _ordersConnection.DisposeAsync();
        await _billingConnection.DisposeAsync();
        await base.Dispose();
    }

    [Fact]
    public void OneContextIsItsOwnUnitOfWork()
    {
        // Arrange
        AddContext<OrdersContext>(_ordersConnection);

        // Act
        var unitOfWork = Target;

        // Assert
        unitOfWork.ShouldBeSameAs(Orders);
    }

    [Fact]
    public void TheSameContextAddedTwiceIsStillItsOwnUnitOfWork()
    {
        // Arrange
        AddContext<OrdersContext>(_ordersConnection);
        AddContext<OrdersContext>(_ordersConnection);

        // Act
        var unitOfWork = Target;

        // Assert
        unitOfWork.ShouldBeSameAs(Orders);
        _builder.Services.Count(descriptor => descriptor.ServiceType == typeof(IUnitOfWork)).ShouldBe(1);
    }

    [Fact]
    public void AnIUnitOfWorkTheHostRegistersAfterAddDbContextWins()
    {
        // Arrange
        AddContext<OrdersContext>(_ordersConnection);
        _builder.Services.AddScoped<IUnitOfWork>(services => services.GetRequiredService<BillingContext>());
        AddContext<BillingContext>(_billingConnection);

        // Act
        var unitOfWork = Target;

        // Assert
        unitOfWork.ShouldBeSameAs(Billing);
    }

    [Fact]
    public async Task SaveChangesAsyncSavesEveryContext()
    {
        // Arrange
        await AddBothContextsAsync();
        Orders.Rows.Add(new OrderRow());
        Billing.Rows.Add(new InvoiceRow());

        // Act
        var written = await Target.SaveChangesAsync(CancellationToken);

        // Assert
        written.ShouldBe(2);
        (await RowCountsAsync()).ShouldBe((1, 1));
    }

    [Fact]
    public async Task ExecuteInTransactionAsyncCommitsEveryContext()
    {
        // Arrange
        await AddBothContextsAsync();

        // Act
        await Target.ExecuteInTransactionAsync(SaveARowInEachContextAsync, CancellationToken);

        // Assert
        (await RowCountsAsync()).ShouldBe((1, 1));
    }

    [Fact]
    public async Task AFailedResultRollsBackEveryContext()
    {
        // Arrange
        await AddBothContextsAsync();

        // Act
        await Target.ExecuteInTransactionAsync(SaveARowInEachContextAsync, static _ => false, CancellationToken);

        // Assert
        (await RowCountsAsync()).ShouldBe((0, 0));
    }

    [Fact]
    public async Task ARetryRunsTheWholeUnitAgainOnEveryContext()
    {
        // Arrange
        await AddBothContextsAsync();
        var attempts = 0;

        // Act
        await Target.ExecuteInTransactionAsync(async cancellationToken =>
        {
            attempts++;
            var written = await SaveARowInEachContextAsync(cancellationToken);
            return attempts == 1 ? throw new TimeoutException("transient") : written;
        }, CancellationToken);

        // Assert
        attempts.ShouldBe(2);
        (await RowCountsAsync()).ShouldBe((1, 1));
    }

    [Fact]
    public async Task TheOutboxContextCommitsLast()
    {
        // Arrange
        AddContext<OrdersContext>(_ordersConnection, enableOutbox: true);
        AddContext<BillingContext>(_billingConnection);
        await CreateSchemasAsync();

        // Act
        await Target.ExecuteInTransactionAsync(SaveARowInEachContextAsync, CancellationToken);

        // Assert
        _commits.ShouldBe([nameof(BillingContext), nameof(OrdersContext)]);
    }

    [Fact]
    public async Task AFailedCommitAfterAnotherContextCommittedThrowsAndIsNotRetried()
    {
        // Arrange
        var failingCommit = new FailingCommitInterceptor();
        AddContext<OrdersContext>(_ordersConnection);
        AddContext<BillingContext>(_billingConnection, interceptor: failingCommit);
        await CreateSchemasAsync();
        failingCommit.IsArmed = true;
        var attempts = 0;

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Target.ExecuteInTransactionAsync(cancellationToken =>
        {
            attempts++;
            return SaveARowInEachContextAsync(cancellationToken);
        }, CancellationToken));

        // Assert
        attempts.ShouldBe(1);
        exception.Message.ShouldContain($"committed '{nameof(OrdersContext)}' but could not commit '{nameof(BillingContext)}'");
        exception.InnerException.ShouldBeOfType<TimeoutException>();
        (await RowCountsAsync()).ShouldBe((1, 0));
    }

    [Fact]
    public async Task AContextWithoutTransactionsStaysOutOfTheUnit()
    {
        // Arrange
        AddContext<OrdersContext>(_ordersConnection);
        _builder.AddDbContext<CatalogContext>(database => database.OnConfigured(configurator =>
            configurator.HostApplicationBuilder.Services.AddDbContext<CatalogContext>(options => options.UseCosmos(CosmosConnectionString, "catalog"))));
        await CreateSchemasAsync();

        // Act
        await Target.ExecuteInTransactionAsync(cancellationToken =>
        {
            Orders.Rows.Add(new OrderRow());
            return Orders.SaveChangesAsync(cancellationToken);
        }, CancellationToken);

        // Assert
        (await RowCountsAsync()).Orders.ShouldBe(1);
    }

    [Fact]
    public async Task AContextWithAnOpenTransactionJoinsItAndTheOthersGetTheirOwn()
    {
        // Arrange
        await AddBothContextsAsync();
        await using var outer = await Orders.Database.BeginTransactionAsync(CancellationToken);

        // Act
        await Target.ExecuteInTransactionAsync(SaveARowInEachContextAsync, static _ => false, CancellationToken);
        await outer.CommitAsync(CancellationToken);

        // Assert
        (await RowCountsAsync()).ShouldBe((1, 0));
    }

    [Fact]
    public async Task ARetryThatWouldRepeatWorkInsideAnOpenTransactionThrows()
    {
        // Arrange
        await AddBothContextsAsync();
        await using var outer = await Orders.Database.BeginTransactionAsync(CancellationToken);
        var attempts = 0;

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Target.ExecuteInTransactionAsync(async cancellationToken =>
        {
            attempts++;
            var written = await SaveARowInEachContextAsync(cancellationToken);
            return attempts == 1 ? throw new TimeoutException("transient") : written;
        }, CancellationToken));

        // Assert
        attempts.ShouldBe(1);
        exception.Message.ShouldContain("a transaction that the unit did not open");
    }

    [Fact]
    public async Task UnsavedChangesFromBeforeTheCallBlockARetry()
    {
        // Arrange
        await AddBothContextsAsync();
        Orders.Rows.Add(new OrderRow());
        var attempts = 0;

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => Target.ExecuteInTransactionAsync(async cancellationToken =>
        {
            attempts++;
            var written = await SaveARowInEachContextAsync(cancellationToken);
            return attempts == 1 ? throw new TimeoutException("transient") : written;
        }, CancellationToken));

        // Assert
        attempts.ShouldBe(1);
        exception.Message.ShouldContain("held unsaved changes before the call");
        (await RowCountsAsync()).ShouldBe((0, 0));
    }

    [Fact]
    public async Task BeginTransactionAsyncSpansEveryContext()
    {
        // Arrange
        AddContext<OrdersContext>(_ordersConnection, withRetries: false);
        AddContext<BillingContext>(_billingConnection, withRetries: false);
        await CreateSchemasAsync();
        await using var transaction = await Target.BeginTransactionAsync(CancellationToken);
        Orders.Rows.Add(new OrderRow());
        Billing.Rows.Add(new InvoiceRow());
        await Target.SaveChangesAsync(CancellationToken);

        // Act
        await transaction.RollbackAsync(CancellationToken);

        // Assert
        (await RowCountsAsync()).ShouldBe((0, 0));
    }

    private async Task AddBothContextsAsync()
    {
        AddContext<OrdersContext>(_ordersConnection);
        AddContext<BillingContext>(_billingConnection);
        await CreateSchemasAsync();
    }

    private void AddContext<TContext>(SqliteConnection connection, bool enableOutbox = false, bool withRetries = true, IInterceptor? interceptor = null)
        where TContext : BaseDbContext
    {
        IInterceptor[] interceptors = interceptor is null
            ? [new CommitRecorder(_commits, typeof(TContext).Name)]
            : [new CommitRecorder(_commits, typeof(TContext).Name), interceptor];

        _builder.AddDbContext<TContext>(database =>
        {
            database.OnConfigured(configurator => configurator.HostApplicationBuilder.Services.AddDbContext<TContext>(options => options
                .UseSqlite(connection, sqlite =>
                {
                    if (withRetries)
                    {
                        sqlite.ExecutionStrategy(dependencies => new RetryOnceExecutionStrategy(dependencies));
                    }
                })
                .AddInterceptors(interceptors)));

            if (enableOutbox)
            {
                database.EnableOutboxProcessing();
            }
        });
    }

    private async Task CreateSchemasAsync()
    {
        await using var scope = _lazyHost.Value.Services.CreateAsyncScope();
        foreach (var context in new DbContext?[] { scope.ServiceProvider.GetService<OrdersContext>(), scope.ServiceProvider.GetService<BillingContext>() })
        {
            if (context is not null)
            {
                await context.Database.EnsureCreatedAsync(CancellationToken);
            }
        }

        _commits.Clear();
    }

    private async Task<int> SaveARowInEachContextAsync(CancellationToken cancellationToken)
    {
        Orders.Rows.Add(new OrderRow());
        Billing.Rows.Add(new InvoiceRow());
        return await Orders.SaveChangesAsync(cancellationToken) + await Billing.SaveChangesAsync(cancellationToken);
    }

    private async Task<(int Orders, int Invoices)> RowCountsAsync()
    {
        await using var scope = _lazyHost.Value.Services.CreateAsyncScope();
        var orders = await scope.ServiceProvider.GetRequiredService<OrdersContext>().Rows.CountAsync(CancellationToken);
        var billing = scope.ServiceProvider.GetService<BillingContext>();
        var invoices = billing is null ? 0 : await billing.Rows.CountAsync(CancellationToken);
        return (orders, invoices);
    }

    public sealed class OrderRow
    {
        public Guid Id { get; init; } = Guid.NewGuid();
    }

    public sealed class InvoiceRow
    {
        public Guid Id { get; init; } = Guid.NewGuid();
    }

    public sealed class OrdersContext(DbContextOptions<OrdersContext> options) : BaseDbContext(options)
    {
        public DbSet<OrderRow> Rows => Set<OrderRow>();

        protected override Assembly? ConfigurationAssembly => null;
    }

    public sealed class BillingContext(DbContextOptions<BillingContext> options) : BaseDbContext(options)
    {
        public DbSet<InvoiceRow> Rows => Set<InvoiceRow>();

        protected override Assembly? ConfigurationAssembly => null;
    }

    public sealed class CatalogContext(DbContextOptions<CatalogContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<OutboxMessage>();
        }
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

    /// <summary>
    /// Records, by context name, every commit that completes.
    /// </summary>
    public sealed class CommitRecorder(ConcurrentQueue<string> commits, string contextName) : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            commits.Enqueue(contextName);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Fails every commit with a transient fault once armed, so the schema can be created first.
    /// </summary>
    public sealed class FailingCommitInterceptor : DbTransactionInterceptor
    {
        public bool IsArmed { get; set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            IsArmed
                ? throw new TimeoutException("commit failed")
                : base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }
}
