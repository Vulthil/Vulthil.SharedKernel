using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Infrastructure.Relational.Tests;

/// <summary>
/// Exercises the composed row-locking fetch on SQLite, which accepts the statement's <c>LIMIT</c> but has no row
/// locks: an empty lock clause proves identifier resolution, ordering and filtering without a provider database, and
/// a real clause proves it reaches the database by making SQLite reject it.
/// </summary>
public sealed class RelationalOutboxStoreRowLockFetchTests : BaseUnitTestCase
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SqliteConnection _renamedConnection = new("DataSource=:memory:");
    private readonly SqliteConnection _unmappedConnection = new("DataSource=:memory:");

    protected override async ValueTask Initialize()
    {
        await _connection.OpenAsync(CancellationToken);
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync(CancellationToken);

        await _renamedConnection.OpenAsync(CancellationToken);
        await using var renamed = NewRenamedContext();
        await renamed.Database.EnsureCreatedAsync(CancellationToken);

        await _unmappedConnection.OpenAsync(CancellationToken);
    }

    protected override async ValueTask Dispose()
    {
        await _connection.DisposeAsync();
        await _renamedConnection.DisposeAsync();
        await _unmappedConnection.DisposeAsync();
    }

    [Fact]
    public async Task FetchComposesTheStatementFromTheMappedIdentifiersOfARenamedOutboxTable()
    {
        // Arrange
        await using (var seed = NewRenamedContext())
        {
            seed.OutboxMessages.Add(NewMessage(Epoch));
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewRenamedContext();
        var store = NewStore(context, string.Empty);

        // Act
        var processed = await store.ProcessBatchAsync((_, _) => Task.FromResult<string?>(null), CancellationToken);

        // Assert
        processed.ShouldBe(1);
        await using var verify = NewRenamedContext();
        (await verify.OutboxMessages.SingleAsync(CancellationToken)).ProcessedOnUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task FetchDispatchesTheOldestMessagesFirstAndStopsAtTheBatchSize()
    {
        // Arrange
        var oldest = NewMessage(Epoch);
        var middle = NewMessage(Epoch.AddHours(1));
        var newest = NewMessage(Epoch.AddHours(2));
        await using (var seed = NewContext())
        {
            seed.OutboxMessages.AddRange(newest, oldest, middle);
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewContext();
        var store = NewStore(context, string.Empty, new OutboxProcessingOptions { BatchSize = 2 });
        var dispatched = new List<Guid>();

        // Act
        var processed = await store.ProcessBatchAsync((message, _) =>
        {
            dispatched.Add(message.Id);
            return Task.FromResult<string?>(null);
        }, CancellationToken);

        // Assert
        processed.ShouldBe(2);
        dispatched.ShouldBe([oldest.Id, middle.Id]);
    }

    [Fact]
    public async Task FetchSkipsProcessedDeadLetteredAndRetryExhaustedMessages()
    {
        // Arrange
        const int maxRetries = 3;
        var pending = NewMessage(Epoch);
        var processedAlready = NewMessage(Epoch.AddMinutes(1));
        processedAlready.ProcessedOnUtc = Epoch.AddMinutes(2);
        var deadLettered = NewMessage(Epoch.AddMinutes(3));
        deadLettered.FailedOnUtc = Epoch.AddMinutes(4);
        var exhausted = NewMessage(Epoch.AddMinutes(5));
        exhausted.RetryCount = maxRetries;
        await using (var seed = NewContext())
        {
            seed.OutboxMessages.AddRange(pending, processedAlready, deadLettered, exhausted);
            await seed.SaveChangesAsync(CancellationToken);
        }

        await using var context = NewContext();
        var store = NewStore(context, string.Empty, new OutboxProcessingOptions { MaxRetries = maxRetries });
        var dispatched = new List<Guid>();

        // Act
        await store.ProcessBatchAsync((message, _) =>
        {
            dispatched.Add(message.Id);
            return Task.FromResult<string?>(null);
        }, CancellationToken);

        // Assert
        dispatched.ShouldBe([pending.Id]);
    }

    [Fact]
    public async Task FetchAppendsTheRowLockClauseToTheStatement()
    {
        // Arrange
        await using var context = NewContext();
        var store = NewStore(context, "FOR UPDATE SKIP LOCKED");

        // Act
        var exception = await Should.ThrowAsync<SqliteException>(
            () => store.ProcessBatchAsync((_, _) => Task.FromResult<string?>(null), CancellationToken));

        // Assert
        exception.Message.ShouldContain("FOR");
    }

    [Fact]
    public async Task FetchThrowsADescriptiveErrorWhenTheContextDoesNotMapTheOutboxEntity()
    {
        // Arrange
        await using var context = new UnmappedOutboxDbContext(new DbContextOptionsBuilder<UnmappedOutboxDbContext>().UseSqlite(_unmappedConnection).Options);
        var store = NewStore(context, string.Empty);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => store.ProcessBatchAsync((_, _) => Task.FromResult<string?>(null), CancellationToken));

        // Assert
        exception.Message.ShouldContain(nameof(UnmappedOutboxDbContext));
        exception.Message.ShouldContain("does not map the OutboxMessage entity");
        exception.Message.ShouldContain("ApplyOutbox");
    }

    private static OutboxMessage NewMessage(DateTimeOffset occurredOnUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        Type = "Test",
        Content = "{}",
        OccurredOnUtc = occurredOnUtc,
        Destination = OutboxDestination.DomainEvent,
    };

    private static RowLockOutboxStore<TContext> NewStore<TContext>(TContext context, string rowLockClause, OutboxProcessingOptions? options = null)
        where TContext : DbContext, ISaveOutboxMessages =>
        new(context, rowLockClause, Options.Create(options ?? new OutboxProcessingOptions()));

    private OutboxDbContext NewContext() => new(new DbContextOptionsBuilder<OutboxDbContext>().UseSqlite(_connection).Options);

    private RenamedOutboxDbContext NewRenamedContext() => new(new DbContextOptionsBuilder<RenamedOutboxDbContext>().UseSqlite(_renamedConnection).Options);

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

    public sealed class RowLockOutboxStore<TContext>(TContext dbContext, string rowLockClause, IOptions<OutboxProcessingOptions> options)
        : RelationalOutboxStore<TContext>(dbContext, TimeProvider.System, options)
        where TContext : DbContext, ISaveOutboxMessages
    {
        protected override Task<List<OutboxMessageData>> FetchMessagesAsync(int batchSize, int maxRetries, CancellationToken cancellationToken) =>
            FetchMessagesWithRowLockAsync(rowLockClause, batchSize, maxRetries, cancellationToken);
    }

    public sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyOutbox();
            ConfigureOutboxDateConversions(modelBuilder);
        }
    }

    public sealed class RenamedOutboxDbContext(DbContextOptions<RenamedOutboxDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyOutbox();
            ConfigureOutboxDateConversions(modelBuilder);

            var entity = modelBuilder.Entity<OutboxMessage>();
            entity.ToTable("outbox_messages");
            entity.Property(message => message.Id).HasColumnName("id");
            entity.Property(message => message.Type).HasColumnName("type");
            entity.Property(message => message.Content).HasColumnName("content");
            entity.Property(message => message.OccurredOnUtc).HasColumnName("occurred_on_utc");
            entity.Property(message => message.ProcessedOnUtc).HasColumnName("processed_on_utc");
            entity.Property(message => message.FailedOnUtc).HasColumnName("failed_on_utc");
            entity.Property(message => message.RetryCount).HasColumnName("retry_count");
        }
    }

    public sealed class UnmappedOutboxDbContext(DbContextOptions<UnmappedOutboxDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<OutboxMessage>();
        }
    }
}
