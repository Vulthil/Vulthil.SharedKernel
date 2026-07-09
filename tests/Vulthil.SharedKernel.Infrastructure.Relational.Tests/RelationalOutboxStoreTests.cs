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

public sealed class RelationalOutboxStoreTests : BaseUnitTestCase
{
    private readonly SqliteConnection _plainConnection = new("DataSource=:memory:");
    private readonly SqliteConnection _unitOfWorkConnection = new("DataSource=:memory:");

    protected override async ValueTask Initialize()
    {
        await _plainConnection.OpenAsync(CancellationToken);
        await using var plain = NewPlainContext();
        await plain.Database.EnsureCreatedAsync(CancellationToken);

        await _unitOfWorkConnection.OpenAsync(CancellationToken);
        await using var unitOfWork = NewUnitOfWorkContext();
        await unitOfWork.Database.EnsureCreatedAsync(CancellationToken);
    }

    protected override async ValueTask Dispose()
    {
        await _plainConnection.DisposeAsync();
        await _unitOfWorkConnection.DisposeAsync();
    }

    [Fact]
    public async Task ProcessBatchThrowsDescriptiveErrorWhenContextDoesNotImplementUnitOfWork()
    {
        // Arrange
        await using var context = NewPlainContext();
        var store = NewPlainStore(context);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => store.ProcessBatchAsync((_, _) => Task.FromResult<string?>(null), CancellationToken));

        // Assert
        exception.Message.ShouldContain(nameof(PlainDbContext));
        exception.Message.ShouldContain("IUnitOfWork");
    }

    [Fact]
    public async Task ProcessBatchOpensATransactionAndProcessesMessagesWhenContextImplementsUnitOfWork()
    {
        // Arrange
        await using var seed = NewUnitOfWorkContext();
        seed.OutboxMessages.Add(NewMessage());
        await seed.SaveChangesAsync(CancellationToken);
        await using var context = NewUnitOfWorkContext();
        var store = NewUnitOfWorkStore(context);

        // Act
        var processed = await store.ProcessBatchAsync((_, _) => Task.FromResult<string?>(null), CancellationToken);

        // Assert
        processed.ShouldBe(1);
        await using var verify = NewUnitOfWorkContext();
        var message = await verify.OutboxMessages.SingleAsync(CancellationToken);
        message.ProcessedOnUtc.ShouldNotBeNull();
    }

    private static OutboxMessage NewMessage() => new()
    {
        Id = Guid.CreateVersion7(),
        Type = "Test",
        Content = "{}",
        OccurredOnUtc = DateTimeOffset.UtcNow,
        Destination = OutboxDestination.DomainEvent,
    };

    private static RelationalOutboxStore<PlainDbContext> NewPlainStore(PlainDbContext context) =>
        new(context, TimeProvider.System, Options.Create(new OutboxProcessingOptions()));

    private static RelationalOutboxStore<UnitOfWorkDbContext> NewUnitOfWorkStore(UnitOfWorkDbContext context) =>
        new(context, TimeProvider.System, Options.Create(new OutboxProcessingOptions()));

    private PlainDbContext NewPlainContext() => new(new DbContextOptionsBuilder<PlainDbContext>().UseSqlite(_plainConnection).Options);

    private UnitOfWorkDbContext NewUnitOfWorkContext() => new(new DbContextOptionsBuilder<UnitOfWorkDbContext>().UseSqlite(_unitOfWorkConnection).Options);

    private static void ConfigureOutboxDateConversions(ModelBuilder modelBuilder)
    {
        var utcConverter = new ValueConverter<DateTimeOffset, DateTime>(
            value => value.UtcDateTime,
            value => new DateTimeOffset(value, TimeSpan.Zero));

        var entity = modelBuilder.Entity<OutboxMessage>();
        entity.Property(message => message.OccurredOnUtc).HasConversion(utcConverter);
        entity.Property(message => message.ProcessedOnUtc).HasConversion(utcConverter);
        entity.Property(message => message.FailedOnUtc).HasConversion(utcConverter);
    }

    public sealed class PlainDbContext(DbContextOptions<PlainDbContext> options) : DbContext(options), ISaveOutboxMessages
    {
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public bool IsInTransaction => Database.CurrentTransaction is not null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.ApplyOutbox();
            ConfigureOutboxDateConversions(modelBuilder);
        }
    }

    public sealed class UnitOfWorkDbContext(DbContextOptions<UnitOfWorkDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyOutbox();
            ConfigureOutboxDateConversions(modelBuilder);
        }
    }
}
