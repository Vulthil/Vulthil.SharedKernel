using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Primitives;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore.Tests;

/// <summary>
/// Drives the relay wake-up through real SQLite saves and transactions with the capture interceptor attached. No
/// relational commit interceptor is attached here, so the tests report transaction outcomes through the public
/// transaction members themselves — the same calls a transaction-capable provider makes.
/// </summary>
public sealed class OutboxRelayWakeupTests : BaseUnitTestCase<OutboxRelayWakeup>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override async ValueTask Initialize()
    {
        await _connection.OpenAsync(CancellationToken);
        await using var context = NewContext(withInterceptor: false);
        await context.Database.EnsureCreatedAsync(CancellationToken);
    }

    protected override async ValueTask Dispose()
    {
        await _connection.DisposeAsync();
        await base.Dispose();
    }

    [Fact]
    public async Task ASaveOutsideATransactionThatCapturesADomainEventWakesTheRelayOnce()
    {
        // Arrange
        await using var context = NewContext();
        context.Aggregates.Add(NewAggregateWithEvent());

        // Act
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Once());
    }

    [Fact]
    public async Task ASynchronousSaveOutsideATransactionThatCapturesADomainEventWakesTheRelayOnce()
    {
        // Arrange
        await using var context = NewContext();
        context.Aggregates.Add(NewAggregateWithEvent());

        // Act
        SaveChangesSynchronously(context);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Once());
    }

    [Fact]
    public async Task ASaveOutsideATransactionThatAddsAnOutboxRowDirectlyWakesTheRelayOnce()
    {
        // Arrange
        await using var context = NewContext();
        context.OutboxMessages.Add(NewMessage());

        // Act
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Once());
    }

    [Fact]
    public async Task ASaveThatInsertsNoOutboxRowsDoesNotWakeTheRelay()
    {
        // Arrange
        await using var context = NewContext();
        context.Aggregates.Add(new TestAggregate(Guid.NewGuid()));

        // Act
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Never());
    }

    [Fact]
    public async Task ARelayMarkingSaveDoesNotWakeTheRelayEvenWhileAnAggregateIsTracked()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        await using (var seed = NewContext(withInterceptor: false))
        {
            seed.Aggregates.Add(new TestAggregate(aggregateId));
            seed.OutboxMessages.Add(NewMessage(messageId));
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewContext();
        await context.Aggregates.SingleAsync(aggregate => aggregate.Id == aggregateId, CancellationToken);
        var failed = await context.OutboxMessages.SingleAsync(message => message.Id == messageId, CancellationToken);
        failed.RetryCount++;
        failed.Error = "broker unavailable";

        // Act
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Never());
    }

    [Fact]
    public async Task AFailedSaveDoesNotWakeTheRelay()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        await using (var seed = NewContext(withInterceptor: false))
        {
            seed.OutboxMessages.Add(NewMessage(existingId));
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewContext();
        context.Aggregates.Add(NewAggregateWithEvent());
        context.OutboxMessages.Add(NewMessage(existingId));

        // Act
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken));

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Never());
    }

    [Fact]
    public async Task ASaveInsideATransactionDoesNotWakeTheRelayBeforeTheCommitIsReported()
    {
        // Arrange
        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
        context.Aggregates.Add(NewAggregateWithEvent());

        // Act
        await context.SaveChangesAsync(CancellationToken);
        await transaction.CommitAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Never());
    }

    [Fact]
    public async Task ACommitReportAfterASaveInsideTheTransactionWakesTheRelayOnce()
    {
        // Arrange
        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
        context.Aggregates.Add(NewAggregateWithEvent());
        await context.SaveChangesAsync(CancellationToken);
        await transaction.CommitAsync(CancellationToken);

        // Act
        Target.TransactionCommitted(context);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Once());
    }

    [Fact]
    public async Task ASecondCommitReportForTheSameTransactionDoesNotWakeTheRelayAgain()
    {
        // Arrange
        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
        context.Aggregates.Add(NewAggregateWithEvent());
        await context.SaveChangesAsync(CancellationToken);
        await transaction.CommitAsync(CancellationToken);
        Target.TransactionCommitted(context);

        // Act
        Target.TransactionCommitted(context);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Once());
    }

    [Fact]
    public async Task ARollbackReportForgetsTheRowsSoALaterCommitReportDoesNotWakeTheRelay()
    {
        // Arrange
        await using var context = NewContext();
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
        context.Aggregates.Add(NewAggregateWithEvent());
        await context.SaveChangesAsync(CancellationToken);
        await transaction.RollbackAsync(CancellationToken);

        // Act
        Target.TransactionRolledBack(context);
        Target.TransactionCommitted(context);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Never());
    }

    [Fact]
    public async Task AStartReportForgetsRowsFromATransactionThatEndedWithoutAReport()
    {
        // Arrange
        await using var context = NewContext();
        await using (await context.Database.BeginTransactionAsync(CancellationToken))
        {
            context.Aggregates.Add(NewAggregateWithEvent());
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        Target.TransactionStarted(context);
        Target.TransactionCommitted(context);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Never());
    }

    private static TestAggregate NewAggregateWithEvent()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseSomething();
        return aggregate;
    }

    private static OutboxMessage NewMessage(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Type = typeof(TestDomainEvent).FullName!,
        Content = "{}",
        OccurredOnUtc = DateTimeOffset.UtcNow,
        Destination = OutboxDestination.DomainEvent,
    };

    private TestDbContext NewContext(bool withInterceptor = true)
    {
        var builder = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection);

        if (withInterceptor)
        {
            builder.AddInterceptors(new DomainEventsToOutboxMessageSaveChangesInterceptor(
                TimeProvider.System,
                Options.Create(new OutboxProcessingOptions()),
                Target));
        }

        return new TestDbContext(builder.Options);
    }

    private static void SaveChangesSynchronously(TestDbContext context) => context.SaveChanges();

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), ISaveOutboxMessages
    {
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

        public bool IsInTransaction => Database.CurrentTransaction is not null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyOutbox();
            modelBuilder.Entity<TestAggregate>().HasKey(aggregate => aggregate.Id);
        }
    }

    public sealed class TestAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void RaiseSomething() => Raise(new TestDomainEvent(Id));
    }

    public sealed record TestDomainEvent(Guid Id) : IDomainEvent;
}
