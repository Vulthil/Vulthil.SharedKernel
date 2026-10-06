using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.Testing;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Exercises the relational outbox store overrides directly on PostgreSQL, the reference dialect: two concurrent
/// stores must claim disjoint batches through <c>FOR UPDATE SKIP LOCKED</c>, successes and failures are recorded
/// set-based, dead-lettering kicks in at the retry limit, dispatch follows the occurred-on order, and the retention
/// delete honors its batch-size contract.
/// </summary>
public sealed class NpgsqlOutboxStoreIntegrationTests(NpgsqlOutboxHostFixture fixture) : BaseUnitTestCase, IClassFixture<NpgsqlOutboxHostFixture>
{
    protected override async ValueTask Dispose() => await fixture.ResetOutboxStateAsync(CancellationToken);

    [Fact]
    public async Task ConcurrentStoresClaimDisjointBatchesThroughSkipLocked()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var seeded = Enumerable.Range(0, 4).Select(offset => NewMessage(baseTime.AddSeconds(offset))).ToList();
        await SeedAsync(seeded);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await using var scopeA = fixture.Services.CreateAsyncScope();
        await using var scopeB = fixture.Services.CreateAsyncScope();
        var storeA = NewStore(scopeA.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>());
        var storeB = NewStore(scopeB.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>());
        var dispatchedA = new List<Guid>();
        var dispatchedB = new List<Guid>();
        var firstDispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstBatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var batchA = storeA.RelayBatchAsync(async (message, cancellationToken) =>
        {
            dispatchedA.Add(message.Id);
            firstDispatchStarted.TrySetResult();
            await releaseFirstBatch.Task.WaitAsync(cancellationToken);
            return null;
        }, timeout.Token, batchSize: 2);
        await firstDispatchStarted.Task.WaitAsync(timeout.Token);
        var processedB = await storeB.RelayBatchAsync((message, _) =>
        {
            dispatchedB.Add(message.Id);
            return Task.FromResult<string?>(null);
        }, timeout.Token, batchSize: 2);
        releaseFirstBatch.SetResult();
        var processedA = await batchA;

        // Assert
        processedA.ShouldBe(2);
        processedB.ShouldBe(2);
        dispatchedA.Intersect(dispatchedB).ShouldBeEmpty();
        dispatchedA.Concat(dispatchedB).Order().ShouldBe(seeded.Select(message => message.Id).Order());
    }

    [Fact]
    public async Task SuccessesAreMarkedProcessedSetBased()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        await SeedAsync(Enumerable.Range(0, 3).Select(offset => NewMessage(baseTime.AddSeconds(offset))).ToList());
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>());
        var dispatched = new List<OutboxMessageData>();

        // Act
        var processed = await store.RelayBatchAsync(RecordingDispatch(dispatched), CancellationToken);

        // Assert
        processed.ShouldBe(3);
        dispatched.Count.ShouldBe(3);
        var rows = await QueryMessagesAsync();
        rows.Count.ShouldBe(3);
        rows.ShouldAllBe(row => row.ProcessedOnUtc != null && row.FailedOnUtc == null && row.RetryCount == 0);
    }

    [Fact]
    public async Task RelayDispatchesMessagesInOccurredOnOrder()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var seeded = Enumerable.Range(0, 6).Select(offset => NewMessage(baseTime.AddSeconds(5 - offset))).ToList();
        await SeedAsync(seeded);
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>());
        var dispatched = new List<OutboxMessageData>();

        // Act
        var processed = await store.RelayBatchAsync(RecordingDispatch(dispatched), CancellationToken);

        // Assert
        processed.ShouldBe(6);
        dispatched.Select(message => message.Id).ShouldBe(Enumerable.Reverse(seeded).Select(message => message.Id));
    }

    [Fact]
    public async Task FailuresIncrementRetryCountAndDeadLetterAtMaxRetries()
    {
        // Arrange
        await SeedAsync([NewMessage(DateTimeOffset.UtcNow)]);
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>());
        var dispatchedAfterDeadLetter = new List<OutboxMessageData>();

        // Act
        var firstBatch = await store.RelayBatchAsync(FailingDispatch("first failure"), CancellationToken, maxRetries: 2);
        var afterFirst = await QuerySingleMessageAsync();
        var secondBatch = await store.RelayBatchAsync(FailingDispatch("second failure"), CancellationToken, maxRetries: 2);
        var afterSecond = await QuerySingleMessageAsync();
        await store.RelayBatchAsync(RecordingDispatch(dispatchedAfterDeadLetter), CancellationToken, maxRetries: 2);

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
    public async Task RetentionDeleteRemovesAtMostBatchSizeRowsPerCallIncludingDeadLetteredRows()
    {
        // Arrange
        var terminalTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var terminalRows = Enumerable.Range(0, 4).Select(offset =>
        {
            var message = NewMessage(terminalTime.AddSeconds(offset));
            message.ProcessedOnUtc = terminalTime.AddSeconds(offset);
            return message;
        }).ToList();
        var deadLettered = NewMessage(terminalTime.AddSeconds(4));
        deadLettered.FailedOnUtc = terminalTime.AddSeconds(4);
        var pending = NewMessage(terminalTime);
        await SeedAsync([.. terminalRows, deadLettered, pending]);
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>());
        var cutoff = terminalTime.AddDays(1);

        // Act
        var firstSweep = await store.DeleteProcessedAsync(cutoff, 2, CancellationToken);
        var secondSweep = await store.DeleteProcessedAsync(cutoff, 2, CancellationToken);
        var thirdSweep = await store.DeleteProcessedAsync(cutoff, 2, CancellationToken);
        var fourthSweep = await store.DeleteProcessedAsync(cutoff, 2, CancellationToken);

        // Assert
        firstSweep.ShouldBe(2);
        secondSweep.ShouldBe(2);
        thirdSweep.ShouldBe(1);
        fourthSweep.ShouldBe(0);
        var remaining = await QueryMessagesAsync();
        remaining.ShouldHaveSingleItem().Id.ShouldBe(pending.Id);
    }

    [Fact]
    public async Task AFailedStatementInOneDispatchStillLetsTheBatchBeRecorded()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var failing = NewMessage(baseTime);
        var succeeding = NewMessage(baseTime.AddSeconds(1));
        await SeedAsync([failing, succeeding]);
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var context = relayScope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>();
        var store = NewStore(context);
        var probeId = Guid.CreateVersion7();

        // Act
        var processed = await store.RelayBatchAsync(async (message, cancellationToken) =>
        {
            if (message.Id == failing.Id)
            {
                await context.Database.ExecuteSqlRawAsync("SELECT 1 / 0", cancellationToken);
            }
            else
            {
                context.Probes.Add(new OutboxProbe(probeId));
            }

            return null;
        }, CancellationToken);

        // Assert
        processed.ShouldBe(1);
        var rows = await QueryMessagesAsync();
        var failed = rows.Single(row => row.Id == failing.Id);
        failed.RetryCount.ShouldBe(1);
        failed.Error.ShouldNotBeNull().ShouldContain("22012");
        rows.Single(row => row.Id == succeeding.Id).ProcessedOnUtc.ShouldNotBeNull();
        (await QueryProbeIdsAsync()).ShouldBe([probeId]);
    }

    [Fact]
    public async Task AFailedSaveInOneDispatchDoesNotFailTheNextDispatch()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var failing = NewMessage(baseTime);
        var succeeding = NewMessage(baseTime.AddSeconds(1));
        await SeedAsync([failing, succeeding]);
        var existingProbeId = await SeedProbeAsync();
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var context = relayScope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>();
        var store = NewStore(context);
        var newProbeId = Guid.CreateVersion7();

        // Act
        var processed = await store.RelayBatchAsync(async (message, cancellationToken) =>
        {
            context.Probes.Add(new OutboxProbe(message.Id == failing.Id ? existingProbeId : newProbeId));
            await context.SaveChangesAsync(cancellationToken);
            return null;
        }, CancellationToken);

        // Assert
        processed.ShouldBe(1);
        var rows = await QueryMessagesAsync();
        rows.Single(row => row.Id == failing.Id).RetryCount.ShouldBe(1);
        rows.Single(row => row.Id == succeeding.Id).ProcessedOnUtc.ShouldNotBeNull();
        (await QueryProbeIdsAsync()).ShouldBe([existingProbeId, newProbeId], ignoreOrder: true);
    }

    private async Task<Guid> SeedProbeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>();
        var probe = new OutboxProbe(Guid.CreateVersion7());
        context.Probes.Add(probe);
        await context.SaveChangesAsync(CancellationToken);
        return probe.Id;
    }

    private async Task<List<Guid>> QueryProbeIdsAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>();
        return await context.Probes.AsNoTracking().Select(probe => probe.Id).ToListAsync(CancellationToken);
    }

    private async Task SeedAsync(IEnumerable<OutboxMessage> messages)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>();
        context.OutboxMessages.AddRange(messages);
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<OutboxMessage> QuerySingleMessageAsync()
    {
        var messages = await QueryMessagesAsync();
        return messages.ShouldHaveSingleItem();
    }

    private async Task<List<OutboxMessage>> QueryMessagesAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NpgsqlOutboxDbContext>();
        return await context.OutboxMessages.AsNoTracking().ToListAsync(CancellationToken);
    }

    private static NpgsqlOutboxStore<NpgsqlOutboxDbContext> NewStore(NpgsqlOutboxDbContext context) =>
        new(context, TimeProvider.System);

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
