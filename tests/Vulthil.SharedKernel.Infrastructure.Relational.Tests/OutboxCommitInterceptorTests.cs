using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Outbox.Testing;
using Vulthil.SharedKernel.Primitives;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Infrastructure.Relational.Tests;

/// <summary>
/// Pins when the relay wakes on a relational provider, with the capture interceptor and the commit interceptor both
/// attached to SQLite and every transaction real: rows saved inside a transaction wake the relay once at the commit,
/// and a commit that saved no outbox rows — the relay's own batch included — never wakes it.
/// </summary>
public sealed class OutboxCommitInterceptorTests : BaseUnitTestCase
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Lazy<OutboxCommitInterceptor> _lazyTarget;

    private OutboxCommitInterceptor Target => _lazyTarget.Value;

    public OutboxCommitInterceptorTests()
    {
        _lazyTarget = new(CreateInstance<OutboxCommitInterceptor>);
        Use(TimeProvider.System);
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions()));
        Use(new OutboxRelayWakeup(GetMock<IOutboxSignal>().Object));
    }

    protected override async ValueTask Initialize()
    {
        await _connection.OpenAsync(CancellationToken);
        await using var context = NewContext(withInterceptors: false);
        await context.Database.EnsureCreatedAsync(CancellationToken);
    }

    protected override async ValueTask Dispose()
    {
        await _connection.DisposeAsync();
        await base.Dispose();
    }

    [Fact]
    public async Task ASaveInsideATransactionWakesTheRelayOnceWhenTheTransactionCommits()
    {
        // Arrange
        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
        context.Aggregates.Add(NewAggregateWithEvent());
        await context.SaveChangesAsync(CancellationToken);
        var wakeUpsBeforeCommit = NotifyCount();

        // Act
        await transaction.CommitAsync(CancellationToken);

        // Assert
        wakeUpsBeforeCommit.ShouldBe(0);
        NotifyCount().ShouldBe(1);
    }

    [Fact]
    public async Task ASynchronousCommitAfterASaveInsideTheTransactionWakesTheRelayOnce()
    {
        // Arrange
        await using var context = NewContext();
        using var transaction = BeginTransactionSynchronously(context);
        context.Aggregates.Add(NewAggregateWithEvent());
        SaveChangesSynchronously(context);

        // Act
        CommitSynchronously(transaction);

        // Assert
        NotifyCount().ShouldBe(1);
    }

    [Fact]
    public async Task ARolledBackTransactionDoesNotWakeTheRelay()
    {
        // Arrange
        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
        context.Aggregates.Add(NewAggregateWithEvent());
        await context.SaveChangesAsync(CancellationToken);

        // Act
        await transaction.RollbackAsync(CancellationToken);

        // Assert
        NotifyCount().ShouldBe(0);
    }

    [Fact]
    public async Task ACommitOfATransactionThatSavedNoOutboxRowsDoesNotWakeTheRelay()
    {
        // Arrange
        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
        context.Aggregates.Add(new WakeupAggregate(Guid.NewGuid()));
        await context.SaveChangesAsync(CancellationToken);

        // Act
        await transaction.CommitAsync(CancellationToken);

        // Assert
        NotifyCount().ShouldBe(0);
    }

    [Fact]
    public async Task ASaveOutsideATransactionThatWritesSeveralRowsWakesTheRelayExactlyOnce()
    {
        // Arrange
        await using var context = NewContext();
        context.Aggregates.AddRange(NewAggregateWithEvent(), NewAggregateWithEvent());

        // Act
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        NotifyCount().ShouldBe(1);
    }

    [Fact]
    public async Task ATransactionDisposedWithoutACommitLeavesNothingForTheNextCommitToWake()
    {
        // Arrange
        await using var context = NewContext();
        await using (await context.Database.BeginTransactionAsync(CancellationToken))
        {
            context.Aggregates.Add(NewAggregateWithEvent());
            await context.SaveChangesAsync(CancellationToken);
        }

        await using var nextTransaction = await context.Database.BeginTransactionAsync(CancellationToken);

        // Act
        await nextTransaction.CommitAsync(CancellationToken);

        // Assert
        NotifyCount().ShouldBe(0);
    }

    [Fact]
    public async Task ARelayBatchWhoseEveryDispatchFailsCommitsTheRetriesWithoutWakingTheRelay()
    {
        // Arrange
        await SeedMessageAsync();
        await using var context = NewContext();
        var store = NewStore(context);

        // Act
        var relayed = await store.RelayBatchAsync((_, _) => Task.FromResult<string?>("broker unavailable"), CancellationToken);

        // Assert
        relayed.ShouldBe(0);
        NotifyCount().ShouldBe(0);
        await using var verify = NewContext(withInterceptors: false);
        (await verify.OutboxMessages.SingleAsync(CancellationToken)).RetryCount.ShouldBe(1);
    }

    [Fact]
    public async Task ASuccessfulRelayBatchDoesNotWakeTheRelay()
    {
        // Arrange
        await SeedMessageAsync();
        await using var context = NewContext();
        var store = NewStore(context);

        // Act
        var relayed = await store.RelayBatchAsync((_, _) => Task.FromResult<string?>(null), CancellationToken);

        // Assert
        relayed.ShouldBe(1);
        NotifyCount().ShouldBe(0);
    }

    [Fact]
    public async Task ARelayBatchWhoseDispatchSavesANewDomainEventWakesTheRelayOnce()
    {
        // Arrange
        await SeedMessageAsync();
        await using var context = NewContext();
        var store = NewStore(context);

        // Act
        var relayed = await store.RelayBatchAsync(async (_, cancellationToken) =>
        {
            context.Aggregates.Add(NewAggregateWithEvent());
            await context.SaveChangesAsync(cancellationToken);
            return null;
        }, CancellationToken);

        // Assert
        relayed.ShouldBe(1);
        NotifyCount().ShouldBe(1);
    }

    private int NotifyCount() => GetMock<IOutboxSignal>().Invocations.Count(invocation => invocation.Method.Name == nameof(IOutboxSignal.Notify));

    private async Task SeedMessageAsync()
    {
        await using var seed = NewContext(withInterceptors: false);
        seed.OutboxMessages.Add(new OutboxMessage
        {
            Type = typeof(WakeupEvent).FullName!,
            Content = "{}",
            OccurredOnUtc = DateTimeOffset.UtcNow,
            Destination = OutboxDestination.DomainEvent,
        });
        await seed.SaveChangesAsync(CancellationToken);
    }

    private static WakeupAggregate NewAggregateWithEvent()
    {
        var aggregate = new WakeupAggregate(Guid.NewGuid());
        aggregate.RaiseCreated();
        return aggregate;
    }

    private static UnlockedOutboxStore NewStore(WakeupDbContext context) => new(context);

    private WakeupDbContext NewContext(bool withInterceptors = true)
    {
        var builder = new DbContextOptionsBuilder<WakeupDbContext>().UseSqlite(_connection);

        if (withInterceptors)
        {
            builder.AddInterceptors(CreateInstance<DomainEventsToOutboxMessageSaveChangesInterceptor>(), Target);
        }

        return new WakeupDbContext(builder.Options);
    }

    private static IDbContextTransaction BeginTransactionSynchronously(WakeupDbContext context) =>
        context.Database.BeginTransaction();

    private static void SaveChangesSynchronously(WakeupDbContext context) => context.SaveChanges();

    private static void CommitSynchronously(IDbContextTransaction transaction) => transaction.Commit();

    /// <summary>
    /// The relational store with an empty lock clause, since SQLite has no row locks.
    /// </summary>
    public sealed class UnlockedOutboxStore(WakeupDbContext dbContext)
        : RelationalOutboxStore<WakeupDbContext>(dbContext, TimeProvider.System)
    {
        protected override Task<List<OutboxMessageData>> FetchMessagesAsync(int batchSize, int maxRetries, CancellationToken cancellationToken) =>
            FetchMessagesWithRowLockAsync(string.Empty, batchSize, maxRetries, cancellationToken);
    }

    public sealed class WakeupDbContext(DbContextOptions<WakeupDbContext> options) : BaseDbContext(options)
    {
        public DbSet<WakeupAggregate> Aggregates => Set<WakeupAggregate>();

        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyOutbox();
            ConfigureOutboxDateConversions(modelBuilder);
            modelBuilder.Entity<WakeupAggregate>().HasKey(aggregate => aggregate.Id);
        }

        /// <summary>
        /// SQLite cannot order or compare <see cref="DateTimeOffset"/> columns, so the outbox timestamps are stored as
        /// UTC <see cref="DateTime"/> values for these tests.
        /// </summary>
        private static void ConfigureOutboxDateConversions(ModelBuilder modelBuilder)
        {
            var utcConverter = new ValueConverter<DateTimeOffset, DateTime>(
                value => value.UtcDateTime,
                value => new DateTimeOffset(value, TimeSpan.Zero));
            var nullableUtcConverter = new ValueConverter<DateTimeOffset?, DateTime?>(
                value => value.HasValue ? value.Value.UtcDateTime : null,
                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

            var entity = modelBuilder.Entity<OutboxMessage>();
            entity.Property(message => message.OccurredOnUtc).HasConversion(utcConverter);
            entity.Property(message => message.ProcessedOnUtc).HasConversion(nullableUtcConverter);
            entity.Property(message => message.FailedOnUtc).HasConversion(nullableUtcConverter);
        }
    }

    public sealed class WakeupAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void RaiseCreated() => Raise(new WakeupEvent(Id));
    }

    public sealed record WakeupEvent(Guid AggregateId) : IDomainEvent;
}
