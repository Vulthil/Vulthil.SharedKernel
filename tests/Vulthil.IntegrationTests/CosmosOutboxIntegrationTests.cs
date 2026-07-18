using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Infrastructure.Cosmos.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Exercises the Cosmos outbox store against the real emulator on a container created by <c>EnsureCreated</c> with
/// default indexing — the stock setup every consumer gets. This settles at runtime whether the relay fetch works on
/// such a container, and covers the base store's failure recording and retention paths on a non-relational provider.
/// </summary>
public sealed class CosmosOutboxIntegrationTests(CosmosWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<CosmosWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<CosmosWebApplicationFactory>
{
    [Fact]
    public async Task RelayFetchesDispatchesAndMarksCapturedMessagesOnAStockContainer()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var first = NewMessage(baseTime);
        var second = NewMessage(baseTime.AddSeconds(1));
        await SeedAsync([second, first]);
        await using var relayScope = Factory.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<CosmosProbeDbContext>());
        var dispatched = new List<OutboxMessageData>();

        // Act
        var processed = await store.ProcessBatchAsync(RecordingDispatch(dispatched), CancellationToken);

        // Assert
        processed.ShouldBe(2);
        dispatched.Select(message => message.Id).ShouldBe(new[] { first.Id, second.Id });
        var rows = await QueryMessagesAsync();
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(row => row.ProcessedOnUtc != null);
    }

    [Fact]
    public async Task FailuresIncrementRetryCountAndDeadLetterAtMaxRetriesThroughTheBaseUpdatePath()
    {
        // Arrange
        await SeedAsync([NewMessage(DateTimeOffset.UtcNow)]);
        await using var relayScope = Factory.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<CosmosProbeDbContext>(), maxRetries: 2);
        var dispatchedAfterDeadLetter = new List<OutboxMessageData>();

        // Act
        var firstBatch = await store.ProcessBatchAsync(FailingDispatch("first failure"), CancellationToken);
        var afterFirst = await QuerySingleMessageAsync();
        var secondBatch = await store.ProcessBatchAsync(FailingDispatch("second failure"), CancellationToken);
        var afterSecond = await QuerySingleMessageAsync();
        await store.ProcessBatchAsync(RecordingDispatch(dispatchedAfterDeadLetter), CancellationToken);

        // Assert
        firstBatch.ShouldBe(0);
        afterFirst.RetryCount.ShouldBe(1);
        afterFirst.Error.ShouldBe("first failure");
        afterFirst.ProcessedOnUtc.ShouldBeNull();
        afterFirst.FailedOnUtc.ShouldBeNull();
        secondBatch.ShouldBe(0);
        afterSecond.RetryCount.ShouldBe(2);
        afterSecond.Error.ShouldBe("second failure");
        afterSecond.ProcessedOnUtc.ShouldBeNull();
        afterSecond.FailedOnUtc.ShouldNotBeNull();
        dispatchedAfterDeadLetter.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetentionDeleteRemovesAtMostBatchSizeRowsPerCall()
    {
        // Arrange
        var terminalTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var processedRows = Enumerable.Range(0, 3).Select(offset =>
        {
            var message = NewMessage(terminalTime.AddSeconds(offset));
            message.ProcessedOnUtc = terminalTime.AddSeconds(offset);
            return message;
        }).ToList();
        var pending = NewMessage(terminalTime);
        await SeedAsync([.. processedRows, pending]);
        await using var relayScope = Factory.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<CosmosProbeDbContext>());
        var cutoff = terminalTime.AddDays(1);

        // Act
        var firstSweep = await store.DeleteProcessedAsync(cutoff, 2, CancellationToken);
        var secondSweep = await store.DeleteProcessedAsync(cutoff, 2, CancellationToken);
        var thirdSweep = await store.DeleteProcessedAsync(cutoff, 2, CancellationToken);

        // Assert
        firstSweep.ShouldBe(2);
        secondSweep.ShouldBe(1);
        thirdSweep.ShouldBe(0);
        var remaining = await QueryMessagesAsync();
        remaining.ShouldHaveSingleItem().Id.ShouldBe(pending.Id);
    }

    private async Task SeedAsync(IEnumerable<OutboxMessage> messages)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var capture = scope.ServiceProvider.GetRequiredService<CosmosProbeDbContext>();
        foreach (var message in messages)
        {
            capture.OutboxMessages.Add(message);
        }

        await capture.SaveChangesAsync(CancellationToken);
    }

    private async Task<OutboxMessage> QuerySingleMessageAsync()
    {
        var messages = await QueryMessagesAsync();
        return messages.ShouldHaveSingleItem();
    }

    private async Task<List<OutboxMessage>> QueryMessagesAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CosmosProbeDbContext>();
        return await context.OutboxMessages.AsNoTracking().ToListAsync(CancellationToken);
    }

    private static CosmosOutboxStore<CosmosProbeDbContext> NewStore(CosmosProbeDbContext context, int maxRetries = 3) =>
        new(context, TimeProvider.System, Options.Create(new OutboxProcessingOptions { MaxRetries = maxRetries }));

    private static OutboxMessage NewMessage(DateTimeOffset occurredOnUtc) => new()
    {
        Type = "TestMessage",
        Content = "{}",
        OccurredOnUtc = occurredOnUtc,
        Destination = OutboxDestination.DomainEvent,
    };

    private static Func<OutboxMessageData, CancellationToken, Task<string?>> RecordingDispatch(List<OutboxMessageData> dispatched) =>
        (message, _) =>
        {
            dispatched.Add(message);
            return Task.FromResult<string?>(null);
        };

    private static Func<OutboxMessageData, CancellationToken, Task<string?>> FailingDispatch(string error) =>
        (_, _) => Task.FromResult<string?>(error);
}
