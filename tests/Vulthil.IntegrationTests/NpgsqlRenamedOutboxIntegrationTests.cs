using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.Testing;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Proves the PostgreSQL outbox works against a model whose outbox table and columns are renamed after
/// <c>ApplyNpgsqlOutbox</c> (as a custom mapping would), instead of assuming the default identifiers: the database
/// builds the pending-message index over the renamed columns, and the relay fetches and marks messages on that table.
/// </summary>
public sealed class NpgsqlRenamedOutboxIntegrationTests(RenamedNpgsqlOutboxHostFixture fixture) : BaseUnitTestCase, IClassFixture<RenamedNpgsqlOutboxHostFixture>
{
    [Fact]
    public async Task EnsureCreatedBuildsThePendingIndexOverTheRenamedColumns()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RenamedNpgsqlOutboxDbContext>();

        // Act
        var predicate = await NpgsqlIndexes.ReadPredicateAsync(context, "IX_OutboxMessages_OccurredOnUtc_Id", CancellationToken);

        // Assert
        predicate.ShouldBe("((processed_on_utc IS NULL) AND (failed_on_utc IS NULL))");
    }

    [Fact]
    public async Task RelayFetchesAndMarksMessagesOnARenamedOutboxTable()
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
            var seedContext = seedScope.ServiceProvider.GetRequiredService<RenamedNpgsqlOutboxDbContext>();
            seedContext.OutboxMessages.Add(message);
            await seedContext.SaveChangesAsync(CancellationToken);
        }

        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = new NpgsqlOutboxStore<RenamedNpgsqlOutboxDbContext>(
            relayScope.ServiceProvider.GetRequiredService<RenamedNpgsqlOutboxDbContext>(),
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
        var verify = verifyScope.ServiceProvider.GetRequiredService<RenamedNpgsqlOutboxDbContext>();
        (await verify.OutboxMessages.AsNoTracking().SingleAsync(CancellationToken)).ProcessedOnUtc.ShouldNotBeNull();
    }
}
