using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Primitives;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore.Tests;

public sealed class DomainEventsToOutboxMessageSaveChangesInterceptorTests : BaseUnitTestCase
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override async ValueTask Initialize()
    {
        await _connection.OpenAsync(CancellationToken);
        await using var context = NewContext(withInterceptor: false);
        await context.Database.EnsureCreatedAsync(CancellationToken);
    }

    protected override async ValueTask Dispose() => await _connection.DisposeAsync();

    [Fact]
    public async Task NonTransactionalSaveCapturingDomainEventsWakesTheRelay()
    {
        // Arrange
        await using var context = NewContext();
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseSomething();
        context.Aggregates.Add(aggregate);

        // Act
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Once());
    }

    [Fact]
    public async Task SaveInsideAnExplicitTransactionDoesNotWakeTheRelay()
    {
        // Arrange
        await using var context = NewContext();
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseSomething();
        context.Aggregates.Add(aggregate);

        // Act
        await using var transaction = await context.Database.BeginTransactionAsync(CancellationToken);
        await context.SaveChangesAsync(CancellationToken);
        await transaction.CommitAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Never());
    }

    [Fact]
    public async Task NonTransactionalSaveWithoutDomainEventsDoesNotWakeTheRelay()
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
    public async Task RelayStyleMarkingSaveWithoutAggregatesDoesNotWakeTheRelay()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        await using (var seed = NewContext(withInterceptor: false))
        {
            seed.OutboxMessages.Add(new OutboxMessage
            {
                Id = messageId,
                Type = typeof(TestDomainEvent).FullName!,
                Content = "{}",
                OccurredOnUtc = DateTimeOffset.UtcNow,
                Destination = OutboxDestination.DomainEvent
            });
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewContext();

        // Act
        var pending = await context.OutboxMessages.SingleAsync(message => message.Id == messageId, CancellationToken);
        pending.RetryCount++;
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        GetMock<IOutboxSignal>().Verify(signal => signal.Notify(), Times.Never());
    }

    private TestDbContext NewContext(bool withInterceptor = true)
    {
        var builder = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection);

        if (withInterceptor)
        {
            builder.AddInterceptors(new DomainEventsToOutboxMessageSaveChangesInterceptor(
                TimeProvider.System,
                Options.Create(new OutboxProcessingOptions()),
                GetMock<IOutboxSignal>().Object));
        }

        return new TestDbContext(builder.Options);
    }

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
