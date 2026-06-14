using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    private static EntityFrameworkOutboxStore<TestDbContext> NewStore(TestDbContext context, int maxRetries) =>
        new(context, TimeProvider.System, Options.Create(new OutboxProcessingOptions { MaxRetries = maxRetries }));

    private static OutboxMessage NewMessage(Guid id, DateTimeOffset occurredOn, DateTimeOffset? failedOnUtc = null) => new()
    {
        Id = id,
        Type = "Test",
        Content = "{}",
        OccurredOnUtc = occurredOn,
        Destination = OutboxDestination.DomainEvent,
        FailedOnUtc = failedOnUtc,
    };

    private TestDbContext NewContext() => new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).Options);

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), ISaveOutboxMessages
    {
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public bool IsInTransaction => Database.CurrentTransaction is not null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration());
            modelBuilder.Entity<OutboxMessage>().Property(message => message.OccurredOnUtc)
                .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(value, TimeSpan.Zero));
        }
    }
}
