using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Primitives;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore.Tests;

public sealed class DomainEventsToOutboxMessageSaveChangesInterceptorTests : BaseUnitTestCase<DomainEventsToOutboxMessageSaveChangesInterceptor>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public DomainEventsToOutboxMessageSaveChangesInterceptorTests()
    {
        Use(TimeProvider.System);
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions()));
        UseReal<OutboxRelayWakeup>();
    }

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
    public async Task AnAsynchronousSaveCapturesEachDomainEventAsAnOutboxRowAndClearsTheAggregate()
    {
        // Arrange
        await using var context = NewContext();
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseSomething();
        aggregate.RaiseSomething();
        context.Aggregates.Add(aggregate);

        // Act
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        aggregate.DomainEvents.ShouldBeEmpty();
        var captured = await context.OutboxMessages.AsNoTracking().ToListAsync(CancellationToken);
        captured.Count.ShouldBe(2);
        captured.ShouldAllBe(message => message.Type == typeof(TestDomainEvent).FullName && message.Destination == OutboxDestination.DomainEvent);
        captured.ShouldAllBe(message => message.Content.Contains(aggregate.Id.ToString()));
    }

    [Fact]
    public async Task ASynchronousSaveCapturesEachDomainEventAsAnOutboxRowAndClearsTheAggregate()
    {
        // Arrange
        await using var context = NewContext();
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.RaiseSomething();
        context.Aggregates.Add(aggregate);

        // Act
        SaveChangesSynchronously(context);

        // Assert
        aggregate.DomainEvents.ShouldBeEmpty();
        var captured = await context.OutboxMessages.AsNoTracking().SingleAsync(CancellationToken);
        captured.Type.ShouldBe(typeof(TestDomainEvent).FullName);
        captured.Content.ShouldContain(aggregate.Id.ToString());
    }

    private TestDbContext NewContext(bool withInterceptor = true)
    {
        var builder = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection);

        if (withInterceptor)
        {
            builder.AddInterceptors(Target);
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
