using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
    public async Task CommitPersistsMarkerAndBusinessWriteTogether()
    {
        // Arrange
        await using var context = NewContext();
        var store = CreateStore(context);

        // Act
        await using (var transaction = await store.BeginAsync(MessageContext, CancellationToken))
        {
            (await transaction.HasProcessedAsync("key-1", CancellationToken)).ShouldBeFalse();
            context.Things.Add(new Thing { Id = 1, Name = "first" });
            await context.SaveChangesAsync(CancellationToken);
            await transaction.CommitAsync("key-1", CancellationToken);
        }

        // Assert
        await using var verify = NewContext();
        (await verify.Things.CountAsync(CancellationToken)).ShouldBe(1);
        (await verify.InboxMessages.AnyAsync(message => message.MessageId == "key-1", CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasProcessedReturnsTrueAfterCommit()
    {
        // Arrange
        await using (var context = NewContext())
        await using (var transaction = await CreateStore(context).BeginAsync(MessageContext, CancellationToken))
        {
            await transaction.CommitAsync("key-1", CancellationToken);
        }

        // Act
        await using var verifyContext = NewContext();
        await using var verifyTransaction = await CreateStore(verifyContext).BeginAsync(MessageContext, CancellationToken);
        var processed = await verifyTransaction.HasProcessedAsync("key-1", CancellationToken);

        // Assert
        processed.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeWithoutCommitRollsBackEverything()
    {
        // Arrange
        await using var context = NewContext();
        var store = CreateStore(context);

        // Act
        await using (var transaction = await store.BeginAsync(MessageContext, CancellationToken))
        {
            context.Things.Add(new Thing { Id = 1, Name = "first" });
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using var verify = NewContext();
        (await verify.Things.AnyAsync(CancellationToken)).ShouldBeFalse();
        (await verify.InboxMessages.AnyAsync(CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task ConcurrentDuplicateKeyIsTreatedAsProcessedAndRollsBackBusinessWrite()
    {
        // Arrange
        await using (var seed = NewContext())
        {
            seed.InboxMessages.Add(new InboxMessage { MessageId = "key-1", ProcessedOnUtc = DateTimeOffset.UtcNow });
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewContext();
        var store = CreateStore(context);

        // Act
        await using (var transaction = await store.BeginAsync(MessageContext, CancellationToken))
        {
            context.Things.Add(new Thing { Id = 2, Name = "second" });
            await context.SaveChangesAsync(CancellationToken);
            await Should.NotThrowAsync(() => transaction.CommitAsync("key-1", CancellationToken));
        }

        // Assert
        await using var verify = NewContext();
        (await verify.Things.AnyAsync(CancellationToken)).ShouldBeFalse();
        (await verify.InboxMessages.CountAsync(CancellationToken)).ShouldBe(1);
    }

    private static IMessageContext MessageContext => Mock.Of<IMessageContext>();

    private TestDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new TestDbContext(options);
    }

    private static RelationalIdempotencyStore<TestDbContext> CreateStore(TestDbContext context) =>
        new(context, TimeProvider.System);

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), ISaveInboxMessages
    {
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        public DbSet<Thing> Things => Set<Thing>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyConfiguration(new InboxMessageEntityConfiguration());
            modelBuilder.Entity<Thing>().HasKey(thing => thing.Id);
        }
    }

    public sealed class Thing
    {
        public int Id { get; set; }

        public required string Name { get; set; }
    }
}
