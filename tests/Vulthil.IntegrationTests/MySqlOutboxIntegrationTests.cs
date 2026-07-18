using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Exercises the MySQL outbox path end to end against a real MySQL server: domain-event capture on
/// <c>SaveChangesAsync</c>, the <c>FOR UPDATE SKIP LOCKED</c> relay fetch, set-based success and failure recording,
/// dead-lettering at the retry limit, the retention-sweep batch contract, and the 256-character Type column cap.
/// </summary>
public sealed class MySqlOutboxIntegrationTests(MySqlOutboxHostFixture fixture) : BaseUnitTestCase, IClassFixture<MySqlOutboxHostFixture>
{
    protected override async ValueTask Dispose() => await fixture.ResetOutboxStateAsync(CancellationToken);

    [Fact]
    public async Task ResolvesTheMySqlStoreAndRelaysACapturedDomainEvent()
    {
        // Arrange
        var probeId = await CaptureProbeCreatedEventAsync();
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = relayScope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var dispatched = new List<OutboxMessageData>();

        // Act
        var processed = await store.ProcessBatchAsync(RecordingDispatch(dispatched), CancellationToken);

        // Assert
        store.ShouldBeOfType<MySqlOutboxStore<MySqlOutboxDbContext>>();
        processed.ShouldBe(1);
        var message = dispatched.ShouldHaveSingleItem();
        message.Type.ShouldBe(typeof(OutboxProbeCreated).FullName);
        message.Content.ShouldContain(probeId.ToString());
        message.Destination.ShouldBe(OutboxDestination.DomainEvent);
        var row = await QuerySingleMessageAsync();
        row.ProcessedOnUtc.ShouldNotBeNull();
        row.FailedOnUtc.ShouldBeNull();
    }

    [Fact]
    public async Task RelayDispatchesMessagesInOccurredOnOrder()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var seeded = Enumerable.Range(0, 6).Select(offset => NewMessage(baseTime.AddSeconds(5 - offset))).ToList();
        await SeedAsync(seeded);
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>());
        var dispatched = new List<OutboxMessageData>();

        // Act
        var processed = await store.ProcessBatchAsync(RecordingDispatch(dispatched), CancellationToken);

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
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>(), maxRetries: 2);
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
        var processedRows = Enumerable.Range(0, 5).Select(offset =>
        {
            var message = NewMessage(terminalTime.AddSeconds(offset));
            message.ProcessedOnUtc = terminalTime.AddSeconds(offset);
            return message;
        }).ToList();
        var pending = NewMessage(terminalTime);
        await SeedAsync([.. processedRows, pending]);
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>());
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
    public async Task TypeColumnAcceptsExactly256CharactersAndRejectsLonger()
    {
        // Arrange
        await SeedAsync([NewMessage(DateTimeOffset.UtcNow, type: new string('t', 256))]);
        await using var captureScope = fixture.Services.CreateAsyncScope();
        var context = captureScope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>();
        context.OutboxMessages.Add(NewMessage(DateTimeOffset.UtcNow, type: new string('t', 257)));

        // Act
        var overflow = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken));

        // Assert
        overflow.InnerException.ShouldNotBeNull();
        var stored = await QueryMessagesAsync();
        stored.ShouldHaveSingleItem().Type.Length.ShouldBe(256);
    }

    private async Task<Guid> CaptureProbeCreatedEventAsync()
    {
        await using var captureScope = fixture.Services.CreateAsyncScope();
        var context = captureScope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>();
        var probe = OutboxProbe.Create();
        context.Probes.Add(probe);
        await context.SaveChangesAsync(CancellationToken);
        return probe.Id;
    }

    private async Task SeedAsync(IEnumerable<OutboxMessage> messages)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>();
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
        var context = scope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>();
        return await context.OutboxMessages.AsNoTracking().ToListAsync(CancellationToken);
    }

    private static MySqlOutboxStore<MySqlOutboxDbContext> NewStore(MySqlOutboxDbContext context, int batchSize = 10, int maxRetries = 3) =>
        new(context, TimeProvider.System, Options.Create(new OutboxProcessingOptions { BatchSize = batchSize, MaxRetries = maxRetries }));

    private static OutboxMessage NewMessage(DateTimeOffset occurredOnUtc, string type = "TestMessage") => new()
    {
        Type = type,
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
