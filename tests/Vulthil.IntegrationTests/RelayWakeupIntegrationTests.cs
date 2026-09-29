using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Pins when a unit of work wakes the outbox relay on a real provider database, through the real <c>Use*</c> plus
/// <c>EnableOutboxProcessing</c> registration: once when rows saved outside a transaction are durable, once when the
/// transaction that saved rows commits, never for a rollback, and never for the relay's own batch commit — the case
/// that otherwise cuts the relay's failure back-off short.
/// </summary>
/// <typeparam name="TFixture">The provider host fixture.</typeparam>
/// <typeparam name="TDbContext">The provider-mapped context, which maps <see cref="OutboxProbe"/>.</typeparam>
public abstract class RelayWakeupIntegrationTests<TFixture, TDbContext>(TFixture fixture) : BaseUnitTestCase
    where TFixture : ProviderOutboxHostFixture<TDbContext>
    where TDbContext : BaseDbContext
{
    protected override async ValueTask Dispose()
    {
        await fixture.ResetOutboxStateAsync(CancellationToken);
        await base.Dispose();
    }

    [Fact]
    public async Task ASaveOutsideATransactionThatCapturesADomainEventWakesTheRelayOnce()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        context.Set<OutboxProbe>().Add(OutboxProbe.Create());

        // Act
        await context.SaveChangesAsync(CancellationToken);

        // Assert
        fixture.Signal.NotifyCount.ShouldBe(1);
    }

    [Fact]
    public async Task ASaveInsideATransactionWakesTheRelayOnceWhenTheTransactionCommits()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Act
        var wakeUpsBeforeCommit = await context.ExecuteInTransactionAsync(async cancellationToken =>
        {
            context.Set<OutboxProbe>().Add(OutboxProbe.Create());
            await context.SaveChangesAsync(cancellationToken);
            return fixture.Signal.NotifyCount;
        }, CancellationToken);

        // Assert
        wakeUpsBeforeCommit.ShouldBe(0);
        fixture.Signal.NotifyCount.ShouldBe(1);
    }

    [Fact]
    public async Task ARolledBackTransactionDoesNotWakeTheRelay()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Act
        await context.ExecuteInTransactionAsync(async cancellationToken =>
        {
            context.Set<OutboxProbe>().Add(OutboxProbe.Create());
            await context.SaveChangesAsync(cancellationToken);
            return false;
        }, shouldCommit: static commit => commit, CancellationToken);

        // Assert
        fixture.Signal.NotifyCount.ShouldBe(0);
        (await QueryMessagesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ARelayBatchWhoseEveryDispatchFailsCommitsTheRetriesWithoutWakingTheRelay()
    {
        // Arrange
        await CaptureProbeCreatedEventAsync();
        fixture.Signal.Reset();
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = relayScope.ServiceProvider.GetRequiredService<IOutboxStore>();

        // Act
        var relayed = await store.ProcessBatchAsync((_, _) => Task.FromResult<string?>("broker unavailable"), CancellationToken);

        // Assert
        relayed.ShouldBe(0);
        fixture.Signal.NotifyCount.ShouldBe(0);
        var row = await QuerySingleMessageAsync();
        row.RetryCount.ShouldBe(1);
        row.Error.ShouldBe("broker unavailable");
    }

    [Fact]
    public async Task ASuccessfulRelayBatchDoesNotWakeTheRelay()
    {
        // Arrange
        await CaptureProbeCreatedEventAsync();
        fixture.Signal.Reset();
        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = relayScope.ServiceProvider.GetRequiredService<IOutboxStore>();

        // Act
        var relayed = await store.ProcessBatchAsync((_, _) => Task.FromResult<string?>(null), CancellationToken);

        // Assert
        relayed.ShouldBe(1);
        fixture.Signal.NotifyCount.ShouldBe(0);
        (await QuerySingleMessageAsync()).ProcessedOnUtc.ShouldNotBeNull();
    }

    private async Task CaptureProbeCreatedEventAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        context.Set<OutboxProbe>().Add(OutboxProbe.Create());
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
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        return await context.OutboxMessages.AsNoTracking().ToListAsync(CancellationToken);
    }
}

/// <summary>
/// Runs the relay wake-up cases on PostgreSQL through <c>UseNpgsql</c>.
/// </summary>
public sealed class NpgsqlRelayWakeupIntegrationTests(NpgsqlOutboxHostFixture fixture)
    : RelayWakeupIntegrationTests<NpgsqlOutboxHostFixture, NpgsqlOutboxDbContext>(fixture), IClassFixture<NpgsqlOutboxHostFixture>;

/// <summary>
/// Runs the relay wake-up cases on MySQL through <c>UseMySql</c>, whose context is pooled on .NET 10.
/// </summary>
public sealed class MySqlRelayWakeupIntegrationTests(MySqlOutboxHostFixture fixture)
    : RelayWakeupIntegrationTests<MySqlOutboxHostFixture, MySqlOutboxDbContext>(fixture), IClassFixture<MySqlOutboxHostFixture>;
