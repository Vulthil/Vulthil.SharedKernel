using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Relational.Tests;

public sealed class RelationalIdempotencyStoreTests : BaseUnitTestCase
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override async ValueTask Initialize()
    {
        await _connection.OpenAsync(CancellationToken);
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync(CancellationToken);
    }

    protected override async ValueTask Dispose() => await _connection.DisposeAsync();

    [Fact]
    public async Task ProcessPersistsMarkerAndBusinessWriteTogether()
    {
        // Arrange
        await using var context = NewContext();
        var store = CreateStore(context);

        // Act
        var processed = await store.ProcessAsync("key-1", MessageContext, async token =>
        {
            context.Things.Add(new Thing { Id = 1, Name = "first" });
            await context.SaveChangesAsync(token);
        }, CancellationToken);

        // Assert
        processed.ShouldBeTrue();
        await using var verify = NewContext();
        (await verify.Things.CountAsync(CancellationToken)).ShouldBe(1);
        (await verify.InboxMessages.AnyAsync(message => message.MessageId == "key-1", CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task AlreadyProcessedKeySkipsConsumerOnSecondDelivery()
    {
        // Arrange
        await using (var first = NewContext())
        {
            await CreateStore(first).ProcessAsync("key-1", MessageContext, _ => Task.CompletedTask, CancellationToken);
        }

        await using var second = NewContext();
        var consumerInvoked = false;

        // Act
        var processed = await CreateStore(second).ProcessAsync("key-1", MessageContext, _ =>
        {
            consumerInvoked = true;
            return Task.CompletedTask;
        }, CancellationToken);

        // Assert
        processed.ShouldBeFalse();
        consumerInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task ConsumerFailureRollsBackMarkerAndBusinessWrite()
    {
        // Arrange
        await using var context = NewContext();
        var store = CreateStore(context);

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => store.ProcessAsync("key-1", MessageContext, async token =>
        {
            context.Things.Add(new Thing { Id = 1, Name = "first" });
            await context.SaveChangesAsync(token);
            throw new InvalidOperationException("consumer failed");
        }, CancellationToken));

        // Assert
        await using var verify = NewContext();
        (await verify.Things.AnyAsync(CancellationToken)).ShouldBeFalse();
        (await verify.InboxMessages.AnyAsync(CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task SeededKeyIsTreatedAsProcessedWithoutRunningConsumer()
    {
        // Arrange
        await using (var seed = NewContext())
        {
            seed.InboxMessages.Add(new InboxMessage { MessageId = "key-1", ProcessedOnUtc = DateTimeOffset.UtcNow });
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewContext();
        var consumerInvoked = false;

        // Act
        var processed = await CreateStore(context).ProcessAsync("key-1", MessageContext, token =>
        {
            consumerInvoked = true;
            context.Things.Add(new Thing { Id = 2, Name = "second" });
            return context.SaveChangesAsync(token);
        }, CancellationToken);

        // Assert
        processed.ShouldBeFalse();
        consumerInvoked.ShouldBeFalse();
        await using var verify = NewContext();
        (await verify.Things.AnyAsync(CancellationToken)).ShouldBeFalse();
        (await verify.InboxMessages.CountAsync(CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ProcessCommitsUnderRetryingExecutionStrategy()
    {
        // Arrange
        await using var context = NewContext(useRetryingStrategy: true);
        var store = CreateStore(context);

        // Act
        var processed = await store.ProcessAsync("key-1", MessageContext, async token =>
        {
            context.Things.Add(new Thing { Id = 1, Name = "first" });
            await context.SaveChangesAsync(token);
        }, CancellationToken);

        // Assert
        processed.ShouldBeTrue();
        await using var verify = NewContext();
        (await verify.Things.CountAsync(CancellationToken)).ShouldBe(1);
        (await verify.InboxMessages.AnyAsync(message => message.MessageId == "key-1", CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteProcessedRemovesOldMarkersButKeepsRecent()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await using (var seed = NewContext())
        {
            seed.InboxMessages.Add(new InboxMessage { MessageId = "old", ProcessedOnUtc = now.AddDays(-10) });
            seed.InboxMessages.Add(new InboxMessage { MessageId = "recent", ProcessedOnUtc = now.AddDays(-1) });
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewContext();
        var store = CreateStore(context);

        // Act
        var deleted = await store.DeleteProcessedAsync(now.AddDays(-7), batchSize: 100, CancellationToken);

        // Assert
        deleted.ShouldBe(1);
        await using var verify = NewContext();
        var remaining = await verify.InboxMessages.Select(marker => marker.MessageId).ToListAsync(CancellationToken);
        remaining.ShouldBe(["recent"]);
    }

    private static IMessageContext MessageContext => Mock.Of<IMessageContext>();

    private TestDbContext NewContext(bool useRetryingStrategy = false)
    {
        var builder = new DbContextOptionsBuilder<TestDbContext>();

        if (useRetryingStrategy)
        {
            builder.UseSqlite(_connection, sqlite => sqlite.ExecutionStrategy(dependencies => new TestRetryingExecutionStrategy(dependencies)));
        }
        else
        {
            builder.UseSqlite(_connection);
        }

        return new TestDbContext(builder.Options);
    }

    private static RelationalIdempotencyStore<TestDbContext> CreateStore(TestDbContext context) =>
        new(context, TimeProvider.System);

    private sealed class TestRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), ISaveInboxMessages
    {
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        public DbSet<Thing> Things => Set<Thing>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyRelationalInbox();
            modelBuilder.Entity<InboxMessage>().Property(message => message.ProcessedOnUtc)
                .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(value, TimeSpan.Zero));
            modelBuilder.Entity<Thing>().HasKey(thing => thing.Id);
        }
    }

    public sealed class Thing
    {
        public int Id { get; set; }

        public required string Name { get; set; }
    }
}
