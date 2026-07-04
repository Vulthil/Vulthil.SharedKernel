using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Relational.Tests;

public sealed class RelationalIdempotencyStoreConflictTests : BaseUnitTestCase
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"vulthil-inbox-conflict-{Guid.NewGuid():N}.db");

    protected override async ValueTask Initialize()
    {
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync(CancellationToken);
    }

    protected override ValueTask Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ConcurrentDeliveriesForTheSameKeyLetExactlyOneCommit()
    {
        // Arrange
        await using var contextA = NewContext();
        await using var contextB = NewContext();

        // Act — SQLite serializes the two own-transaction attempts under the hood (a shared busy timeout
        // resolves the lock contention instead of failing fast), so of two genuinely concurrent deliveries
        // for the same key, exactly one commits its business write and marker; the loser observes the
        // marker as already processed and never runs its consumer body.
        var taskA = CreateStore(contextA).ProcessAsync("race-key", MessageContext, async token =>
        {
            contextA.Things.Add(new Thing { Id = 1, Name = "a" });
            await contextA.SaveChangesAsync(token);
        }, CancellationToken);

        var taskB = CreateStore(contextB).ProcessAsync("race-key", MessageContext, async token =>
        {
            contextB.Things.Add(new Thing { Id = 2, Name = "b" });
            await contextB.SaveChangesAsync(token);
        }, CancellationToken);

        var results = await Task.WhenAll(taskA, taskB);

        // Assert
        results.Count(processed => processed).ShouldBe(1);
        await using var verify = NewContext();
        (await verify.Things.CountAsync(CancellationToken)).ShouldBe(1);
        (await verify.InboxMessages.CountAsync(CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task AmbientTransactionConflictPropagatesInsteadOfCommittingDuplicateWork()
    {
        // Act
        var propagated = await RunAmbientConflictScenarioAsync();

        // Assert — a marker conflict inside an ambient transaction must never let the caller commit this
        // delivery's business write: it either surfaces so the caller rolls back (fixed) or is silently
        // absorbed while the caller commits anyway (the defect this pins).
        propagated.ShouldNotBeNull();
        await using var verify = NewContext();
        (await verify.Things.AnyAsync(thing => thing.Name == "losing-consumer-write", CancellationToken)).ShouldBeFalse();
        (await verify.InboxMessages.AnyAsync(CancellationToken)).ShouldBeFalse();
    }

    private async Task<DbUpdateException?> RunAmbientConflictScenarioAsync()
    {
        await using var outerContext = NewContext();
        await using var outerTransaction = await outerContext.Database.BeginTransactionAsync(CancellationToken);
        var store = CreateStore(outerContext);

        try
        {
            await store.ProcessAsync("race-key", MessageContext, async token =>
            {
                outerContext.Things.Add(new Thing { Id = 1, Name = "losing-consumer-write" });
                await outerContext.SaveChangesAsync(token);

                await InsertConflictingMarkerOnTheSameTransactionAsync(outerContext, outerTransaction, token);
            }, CancellationToken);

            await outerTransaction.CommitAsync(CancellationToken);
            return null;
        }
        catch (DbUpdateException exception)
        {
            await outerTransaction.RollbackAsync(CancellationToken);
            return exception;
        }
    }

    private static async Task InsertConflictingMarkerOnTheSameTransactionAsync(
        TestDbContext outerContext, IDbContextTransaction outerTransaction, CancellationToken cancellationToken)
    {
        await using var conflictContext = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>().UseSqlite(outerContext.Database.GetDbConnection()).Options);
        await conflictContext.Database.UseTransactionAsync(outerTransaction.GetDbTransaction(), cancellationToken);
        conflictContext.InboxMessages.Add(new InboxMessage { MessageId = "race-key", ProcessedOnUtc = TimeProvider.System.GetUtcNow() });
        await conflictContext.SaveChangesAsync(cancellationToken);
    }

    private static IMessageContext MessageContext => Mock.Of<IMessageContext>();

    private TestDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = _databasePath, DefaultTimeout = 5 }.ToString())
            .Options);

    private static RelationalIdempotencyStore<TestDbContext> CreateStore(TestDbContext context) =>
        new(context, TimeProvider.System);

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
