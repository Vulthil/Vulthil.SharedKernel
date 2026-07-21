using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;
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
        var store = NewStore(context, maxRetries: 1);

        // Act
        await store.ProcessBatchAsync((_, _) => Task.FromResult<string?>("boom"), CancellationToken);

        // Assert
        await using var verify = NewContext();
        var message = await verify.OutboxMessages.SingleAsync(CancellationToken);
        message.FailedOnUtc.ShouldNotBeNull();
        message.ProcessedOnUtc.ShouldBeNull();
        message.RetryCount.ShouldBe(1);
        message.Error.ShouldBe("boom");
    }

    [Fact]
    public async Task DoesNotFetchADeadLetteredMessage()
    {
        // Arrange
        await using var seed = NewContext();
        seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), DateTimeOffset.UtcNow, failedOnUtc: DateTimeOffset.UtcNow));
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context, maxRetries: 3);
        var dispatched = new List<Guid>();

        // Act
        var processed = await store.ProcessBatchAsync((data, _) =>
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
        var store = NewStore(context, maxRetries: 3);
        var dispatchCount = 0;

        // Act
        var processed = await store.ProcessBatchAsync((_, _) =>
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
        var store = NewStore(context, maxRetries: 3);
        var dispatched = new List<Guid>();

        // Act
        await store.ProcessBatchAsync((data, _) =>
        {
            dispatched.Add(data.Id);
            return Task.FromResult<string?>(null);
        }, CancellationToken);

        // Assert
        dispatched.ShouldBe([first, second]);
    }

    [Fact]
    public async Task ThrottlesParallelDispatchToTheConfiguredMaxDegreeOfParallelism()
    {
        // Arrange
        await using var seed = NewContext();
        for (var i = 0; i < 6; i++)
        {
            seed.OutboxMessages.Add(NewMessage(Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        }
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context, maxRetries: 3, enableParallelPublishing: true, maxDegreeOfParallelism: 2);
        var dispatcher = new ConcurrencyTrackingDispatcher(saturationCount: 2);

        // Act
        var processTask = store.ProcessBatchAsync((_, token) => dispatcher.DispatchAsync(token), CancellationToken);
        await dispatcher.SaturationReached.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken);
        dispatcher.Release();
        var processed = await processTask;

        // Assert
        processed.ShouldBe(6);
        dispatcher.PeakConcurrency.ShouldBe(2);
    }

    [Fact]
    public async Task RecordsEachMessageOutcomeIndividuallyWhenAParallelBatchHasAFailure()
    {
        // Arrange
        var failing = new Guid("00000000-0000-0000-0000-000000000001");
        await using var seed = NewContext();
        seed.OutboxMessages.Add(NewMessage(failing, DateTimeOffset.UtcNow));
        seed.OutboxMessages.Add(NewMessage(new Guid("00000000-0000-0000-0000-000000000002"), DateTimeOffset.UtcNow));
        seed.OutboxMessages.Add(NewMessage(new Guid("00000000-0000-0000-0000-000000000003"), DateTimeOffset.UtcNow));
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewContext();
        var store = NewStore(context, maxRetries: 3, enableParallelPublishing: true);

        // Act
        var processed = await store.ProcessBatchAsync(
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
        var store = NewStore(context, maxRetries: 3);

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

    private static EntityFrameworkOutboxStore<TestDbContext> NewStore(TestDbContext context, int maxRetries, bool enableParallelPublishing = false, int maxDegreeOfParallelism = 4) =>
        new(context, TimeProvider.System, Options.Create(new OutboxProcessingOptions
        {
            MaxRetries = maxRetries,
            EnableParallelPublishing = enableParallelPublishing,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
        }));

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

    private sealed class ConcurrencyTrackingDispatcher(int saturationCount)
    {
        private readonly TaskCompletionSource _saturated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _inFlight;
        private int _peak;
        private int _arrivals;

        public Task SaturationReached => _saturated.Task;

        public int PeakConcurrency => Volatile.Read(ref _peak);

        public async Task<string?> DispatchAsync(CancellationToken cancellationToken)
        {
            RecordArrival();
            await _gate.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref _inFlight);
            return null;
        }

        public void Release() => _gate.TrySetResult();

        private void RecordArrival()
        {
            UpdatePeak(Interlocked.Increment(ref _inFlight));
            if (Interlocked.Increment(ref _arrivals) >= saturationCount)
            {
                _saturated.TrySetResult();
            }
        }

        private void UpdatePeak(int current)
        {
            var seen = Volatile.Read(ref _peak);
            while (current > seen)
            {
                var previous = Interlocked.CompareExchange(ref _peak, current, seen);
                if (previous == seen)
                {
                    return;
                }

                seen = previous;
            }
        }
    }

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
