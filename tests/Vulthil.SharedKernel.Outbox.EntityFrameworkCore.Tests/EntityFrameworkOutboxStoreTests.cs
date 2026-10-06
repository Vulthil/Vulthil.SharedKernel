using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vulthil.SharedKernel.Outbox.Testing;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore.Tests;

public sealed class EntityFrameworkOutboxStoreTests : BaseUnitTestCase
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
    public async Task DeadLettersAMessageAfterExhaustingRetries()
    {
        // Arrange
        await using var seed = NewContext();
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context);

        // Act
        await store.RelayBatchAsync((_, _) => Task.FromResult<string?>("boom"), CancellationToken, maxRetries: 1);

        // Assert
        await using var verify = NewContext();
        var message = await verify.OutboxMessages.SingleAsync(CancellationToken);
        message.FailedOnUtc.ShouldNotBeNull();
        message.ProcessedOnUtc.ShouldBeNull();
        message.RetryCount.ShouldBe(1);
        message.Error.ShouldBe("boom");
    }

    [Fact]
    public async Task AFailedStepForgetsTheChangesItLeftPendingWhileASucceededStepSavesThem()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var failing = NewMessage(Guid.CreateVersion7(), baseTime);
        var succeeding = NewMessage(Guid.CreateVersion7(), baseTime.AddSeconds(1));
        await using var seed = NewContext();
        seed.OutboxMessages.AddRange(failing, succeeding);
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context);
        var leftOver = NewMessage(Guid.CreateVersion7(), baseTime.AddSeconds(2));
        var kept = NewMessage(Guid.CreateVersion7(), baseTime.AddSeconds(3));

        // Act
        var processed = await store.RelayBatchAsync((data, _) =>
        {
            if (data.Id == failing.Id)
            {
                context.OutboxMessages.Add(leftOver);
                return Task.FromResult<string?>("boom");
            }

            context.OutboxMessages.Add(kept);
            return Task.FromResult<string?>(null);
        }, CancellationToken);

        // Assert
        processed.ShouldBe(1);
        await using var verify = NewContext();
        var ids = await verify.OutboxMessages.Select(message => message.Id).ToListAsync(CancellationToken);
        ids.ShouldBe([failing.Id, succeeding.Id, kept.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task DoesNotFetchADeadLetteredMessage()
    {
        // Arrange
        await using var seed = NewContext();
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), DateTimeOffset.UtcNow, failedOnUtc: DateTimeOffset.UtcNow));
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context);
        var dispatched = new List<Guid>();

        // Act
        var processed = await store.RelayBatchAsync((data, _) =>
        {
            dispatched.Add(data.Id);
            return Task.FromResult<string?>(null);
        }, CancellationToken);

        // Assert
        processed.ShouldBe(0);
        dispatched.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsOnlyTheSuccessfullyDispatchedCountWhenTheBatchHasFailures()
    {
        // Arrange
        await using var seed = NewContext();
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context);
        var dispatchCount = 0;

        // Act
        var processed = await store.RelayBatchAsync((_, _) =>
        {
            dispatchCount++;
            return Task.FromResult<string?>(dispatchCount == 1 ? "boom" : null);
        }, CancellationToken);

        // Assert
        processed.ShouldBe(1);
    }

    [Fact]
    public async Task FetchesMessagesOrderedByOccurredOnThenId()
    {
        // Arrange
        var occurredOn = DateTimeOffset.UtcNow;
        var first = new Guid("00000000-0000-0000-0000-000000000001");
        var second = new Guid("00000000-0000-0000-0000-000000000002");
        await using var seed = NewContext();
        seed.OutboxMessages.Add(NewMessage(second, occurredOn));
        seed.OutboxMessages.Add(NewMessage(first, occurredOn));
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context);
        var dispatched = new List<Guid>();

        // Act
        await store.RelayBatchAsync((data, _) =>
        {
            dispatched.Add(data.Id);
            return Task.FromResult<string?>(null);
        }, CancellationToken);

        // Assert
        dispatched.ShouldBe([first, second]);
    }

    [Fact]
    public async Task RecordsEachMessageOutcomeIndividuallyWhenABatchHasAFailure()
    {
        // Arrange
        var failing = new Guid("00000000-0000-0000-0000-000000000001");
        await using var seed = NewContext();
        seed.OutboxMessages.Add(NewMessage(failing, DateTimeOffset.UtcNow));
        seed.OutboxMessages.Add(NewMessage(new Guid("00000000-0000-0000-0000-000000000002"), DateTimeOffset.UtcNow));
        seed.OutboxMessages.Add(NewMessage(new Guid("00000000-0000-0000-0000-000000000003"), DateTimeOffset.UtcNow));
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context);

        // Act
        var processed = await store.RelayBatchAsync(
            (data, _) => Task.FromResult<string?>(data.Id == failing ? "boom" : null),
            CancellationToken);

        // Assert
        processed.ShouldBe(2);
        await using var verify = NewContext();
        var failed = await verify.OutboxMessages.SingleAsync(message => message.Id == failing, CancellationToken);
        failed.ProcessedOnUtc.ShouldBeNull();
        failed.FailedOnUtc.ShouldBeNull();
        failed.RetryCount.ShouldBe(1);
        failed.Error.ShouldBe("boom");
        var succeeded = await verify.OutboxMessages.Where(message => message.Id != failing).ToListAsync(CancellationToken);
        succeeded.Count.ShouldBe(2);
        foreach (var message in succeeded)
        {
            message.ProcessedOnUtc.ShouldNotBeNull();
            message.FailedOnUtc.ShouldBeNull();
            message.RetryCount.ShouldBe(0);
            message.Error.ShouldBeNull();
        }
    }

    [Fact]
    public async Task DeleteProcessedRemovesOldTerminalRowsButKeepsRecentAndPending()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await using var seed = NewContext();
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), now.AddDays(-10), processedOnUtc: now.AddDays(-10)));
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), now.AddDays(-10), failedOnUtc: now.AddDays(-10)));
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), now.AddDays(-1), processedOnUtc: now.AddDays(-1)));
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), now));
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context);

        // Act
        var deleted = await store.DeleteProcessedAsync(now.AddDays(-7), batchSize: 100, CancellationToken);

        // Assert
        deleted.ShouldBe(2);
        await using var verify = NewContext();
        var remaining = await verify.OutboxMessages.ToListAsync(CancellationToken);
        remaining.Count.ShouldBe(2);
        remaining.ShouldContain(message => message.ProcessedOnUtc == null);
        remaining.ShouldContain(message => message.ProcessedOnUtc >= now.AddDays(-7));
    }

    private static EntityFrameworkOutboxStore<TestDbContext> NewStore(TestDbContext context) =>
        new(context, TimeProvider.System);

    private static OutboxMessage NewMessage(Guid id, DateTimeOffset occurredOn, DateTimeOffset? failedOnUtc = null, DateTimeOffset? processedOnUtc = null) => new()
    {
        Id = id,
        Type = "Test",
        Content = "{}",
        OccurredOnUtc = occurredOn,
        Destination = OutboxDestination.DomainEvent,
        FailedOnUtc = failedOnUtc,
        ProcessedOnUtc = processedOnUtc,
    };

    private TestDbContext NewContext() => new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).Options);

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), ISaveOutboxMessages
    {
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public bool IsInTransaction => Database.CurrentTransaction is not null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyOutbox();

            var utcConverter = new ValueConverter<DateTimeOffset, DateTime>(
                value => value.UtcDateTime,
                value => new DateTimeOffset(value, TimeSpan.Zero));

            var entity = modelBuilder.Entity<OutboxMessage>();
            entity.Property(message => message.OccurredOnUtc).HasConversion(utcConverter);
            entity.Property(message => message.ProcessedOnUtc).HasConversion(utcConverter);
            entity.Property(message => message.FailedOnUtc).HasConversion(utcConverter);
        }
    }
}
