using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.Npgsql;
using Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.Testing;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Proves the PostgreSQL outbox works under <c>UseSnakeCaseNamingConvention</c>, registered through <c>UseNpgsql</c>:
/// the database builds the pending-message index over the snake_case columns, and the relay fetches and marks
/// messages on that table.
/// </summary>
public sealed class NpgsqlSnakeCaseOutboxIntegrationTests(NpgsqlSnakeCaseOutboxIntegrationTests.SnakeCaseNpgsqlOutboxHostFixture fixture)
    : BaseUnitTestCase, IClassFixture<NpgsqlSnakeCaseOutboxIntegrationTests.SnakeCaseNpgsqlOutboxHostFixture>
{
    [Fact]
    public async Task EnsureCreatedBuildsThePendingIndexOverTheSnakeCaseColumns()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnakeCaseNpgsqlOutboxDbContext>();

        // Act
        var predicate = await NpgsqlIndexes.ReadPredicateAsync(context, "IX_OutboxMessages_OccurredOnUtc_Id", CancellationToken);

        // Assert
        predicate.ShouldBe("((processed_on_utc IS NULL) AND (failed_on_utc IS NULL))");
    }

    [Fact]
    public async Task RelayFetchesAndMarksMessagesOnASnakeCaseOutboxTable()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Type = "TestMessage",
            Content = "{}",
            OccurredOnUtc = DateTimeOffset.UtcNow,
            Destination = OutboxDestination.DomainEvent,
        };
        await using (var seedScope = fixture.Services.CreateAsyncScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<SnakeCaseNpgsqlOutboxDbContext>();
            seedContext.OutboxMessages.Add(message);
            await seedContext.SaveChangesAsync(CancellationToken);
        }

        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = new NpgsqlOutboxStore<SnakeCaseNpgsqlOutboxDbContext>(
            relayScope.ServiceProvider.GetRequiredService<SnakeCaseNpgsqlOutboxDbContext>(),
            TimeProvider.System);
        var dispatched = new List<OutboxMessageData>();

        // Act
        var processed = await store.RelayBatchAsync((data, _) =>
        {
            dispatched.Add(data);
            return Task.FromResult<string?>(null);
        }, CancellationToken);

        // Assert
        processed.ShouldBe(1);
        dispatched.ShouldHaveSingleItem().Id.ShouldBe(message.Id);
        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<SnakeCaseNpgsqlOutboxDbContext>();
        (await verify.OutboxMessages.AsNoTracking().SingleAsync(CancellationToken)).ProcessedOnUtc.ShouldNotBeNull();
    }

    public sealed class SnakeCaseNpgsqlOutboxDbContext(DbContextOptions<SnakeCaseNpgsqlOutboxDbContext> options) : BaseDbContext(options)
    {
        protected override Assembly? ConfigurationAssembly => null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyNpgsqlOutbox();
        }
    }

    public sealed class SnakeCaseNpgsqlOutboxHostFixture(IntegrationTestContainerHost containerHost)
        : ProviderOutboxHostFixture<SnakeCaseNpgsqlOutboxDbContext>(containerHost)
    {
        protected override ITestContainer SelectContainer(IntegrationTestContainerHost host) =>
            host.Containers.OfType<PostgreSqlTestContainer>().Single();

        protected override void RegisterDatabase(IHostApplicationBuilder builder, string connectionStringKey) =>
            builder.AddDbContext<SnakeCaseNpgsqlOutboxDbContext>(database => database
                .UseNpgsql(connectionStringKey, configureDbContextOptions: options => options.UseSnakeCaseNamingConvention())
                .EnableOutboxProcessing());
    }
}
