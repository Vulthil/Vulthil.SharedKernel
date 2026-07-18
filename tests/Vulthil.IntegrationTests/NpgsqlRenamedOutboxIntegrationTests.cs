using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Proves the PostgreSQL relay fetch works against a model whose outbox table and columns are renamed (as
/// <c>UseSnakeCaseNamingConvention</c> or a custom mapping would), instead of assuming the default identifiers.
/// </summary>
public sealed class NpgsqlRenamedOutboxIntegrationTests(RenamedNpgsqlOutboxHostFixture fixture) : BaseUnitTestCase, IClassFixture<RenamedNpgsqlOutboxHostFixture>
{
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
            TimeProvider.System,
            Options.Create(new OutboxProcessingOptions()));
        var dispatched = new List<OutboxMessageData>();

        // Act
        var processed = await store.ProcessBatchAsync((data, _) =>
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
