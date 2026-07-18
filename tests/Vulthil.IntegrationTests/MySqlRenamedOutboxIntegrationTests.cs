using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Proves the MySQL relay fetch works against a model whose outbox table and columns are renamed (as a naming
/// convention or custom mapping would), instead of assuming the default identifiers.
/// </summary>
public sealed class MySqlRenamedOutboxIntegrationTests(RenamedMySqlOutboxHostFixture fixture) : BaseUnitTestCase, IClassFixture<RenamedMySqlOutboxHostFixture>
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
            var seedContext = seedScope.ServiceProvider.GetRequiredService<RenamedMySqlOutboxDbContext>();
            seedContext.OutboxMessages.Add(message);
            await seedContext.SaveChangesAsync(CancellationToken);
        }

        await using var relayScope = fixture.Services.CreateAsyncScope();
        var store = new MySqlOutboxStore<RenamedMySqlOutboxDbContext>(
            relayScope.ServiceProvider.GetRequiredService<RenamedMySqlOutboxDbContext>(),
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
        var verify = verifyScope.ServiceProvider.GetRequiredService<RenamedMySqlOutboxDbContext>();
        (await verify.OutboxMessages.AsNoTracking().SingleAsync(CancellationToken)).ProcessedOnUtc.ShouldNotBeNull();
    }
}
