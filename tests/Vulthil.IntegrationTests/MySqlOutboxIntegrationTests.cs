using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.Testing;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Exercises the MySQL outbox path end to end against a real MySQL server: domain-event capture on
/// <c>SaveChangesAsync</c>, the <c>FOR UPDATE SKIP LOCKED</c> relay fetch, set-based success and failure recording,
/// dead-lettering at the retry limit, the retention-sweep batch contract, and a domain event whose type name is longer
/// than 256 characters.
/// </summary>
public sealed class MySqlOutboxIntegrationTests(MySqlOutboxHostFixture fixture) : BaseUnitTestCase, IClassFixture<MySqlOutboxHostFixture>
{
    protected override async ValueTask Dispose() => await fixture.ResetOutboxStateAsync(CancellationToken);

    [Fact]
    public async Task ResolvesTheMySqlStoreAndRelaysACapturedDomainEvent()
    {
        // Arrange
        var probeId = await CaptureAsync(OutboxProbe.Create());
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = relayScope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var dispatched = new List<OutboxMessageData>();

        // Act
        var processed = await store.RelayBatchAsync(RecordingDispatch(dispatched), CancellationToken);

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
        var store = NewStore(relayScope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>());
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
    public async Task ADomainEventWhoseTypeNameIsLongerThan256CharactersIsCapturedAndRelayed()
    {
        // Arrange
        var eventType = typeof(ProbeStatusChanged<Pending, Settled>);
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = relayScope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var dispatched = new List<OutboxMessageData>();

        // Act
        await CaptureAsync(OutboxProbe.Create(static probeId =>
            new ProbeStatusChanged<Pending, Settled>(probeId, new Pending("Awaiting payment"), new Settled("PAY-1"))));
        var processed = await store.RelayBatchAsync(RecordingDispatch(dispatched), CancellationToken);

        // Assert
        eventType.FullName.ShouldNotBeNull().Length.ShouldBeGreaterThan(256);
        processed.ShouldBe(1);
        var message = dispatched.ShouldHaveSingleItem();
        message.Type.ShouldBe(eventType.FullName);
        OutboxMessageTypes.Resolve(message.Type).ShouldBe(eventType);
        var row = await QuerySingleMessageAsync();
        row.ProcessedOnUtc.ShouldNotBeNull();
    }

    private async Task<Guid> CaptureAsync(OutboxProbe probe)
    {
        await using var captureScope = fixture.Services.CreateAsyncScope();
        var context = captureScope.ServiceProvider.GetRequiredService<MySqlOutboxDbContext>();
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

    private static MySqlOutboxStore<MySqlOutboxDbContext> NewStore(MySqlOutboxDbContext context) =>
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

    public sealed record ProbeStatusChanged<TFrom, TTo>(Guid ProbeId, TFrom From, TTo To) : IDomainEvent;

    public sealed record Pending(string Reason);

    public sealed record Settled(string Reference);
}
