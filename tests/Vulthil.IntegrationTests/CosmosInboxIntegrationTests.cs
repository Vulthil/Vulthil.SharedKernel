using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Inbox;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Exercises the Cosmos idempotency store against the real Cosmos emulator (through the
/// <see cref="CosmosWebApplicationFactory"/> pipeline): a first delivery runs the consumer and records the marker,
/// and a duplicate delivery for the same key is skipped — giving effectively-once processing.
/// </summary>
public sealed class CosmosInboxIntegrationTests(CosmosWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<CosmosWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<CosmosWebApplicationFactory>
{
    private static readonly IMessageContext _messageContext = Mock.Of<IMessageContext>();

    [Fact]
    public async Task FirstDeliveryRunsConsumerAndRecordsMarker()
    {
        // Arrange
        const string key = "order-1";
        var store = ScopedServices.GetRequiredService<IIdempotencyStore>();

        // Act
        var processed = await store.ProcessAsync(key, _messageContext, WriteSideEffect(key), CancellationToken);

        // Assert
        processed.ShouldBeTrue();
        await ResetScope();
        var verify = ScopedServices.GetRequiredService<CosmosProbeDbContext>();
        (await verify.SideEffects.CountAsync(CancellationToken)).ShouldBe(1);
        (await verify.InboxMessages.CountAsync(CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task DuplicateDeliveryIsSkippedAndConsumerRunsOnce()
    {
        // Arrange
        const string key = "order-2";
        var firstProcessed = await ScopedServices.GetRequiredService<IIdempotencyStore>()
            .ProcessAsync(key, _messageContext, WriteSideEffect(key), CancellationToken);
        await ResetScope();

        // Act
        var consumerRanAgain = false;
        var secondProcessed = await ScopedServices.GetRequiredService<IIdempotencyStore>()
            .ProcessAsync(key, _messageContext, async token =>
            {
                consumerRanAgain = true;
                await WriteSideEffect(key)(token);
            }, CancellationToken);

        // Assert
        firstProcessed.ShouldBeTrue();
        secondProcessed.ShouldBeFalse();
        consumerRanAgain.ShouldBeFalse();
        await ResetScope();
        var verify = ScopedServices.GetRequiredService<CosmosProbeDbContext>();
        (await verify.SideEffects.CountAsync(CancellationToken)).ShouldBe(1);
        (await verify.InboxMessages.CountAsync(CancellationToken)).ShouldBe(1);
    }

    private Func<CancellationToken, Task> WriteSideEffect(string key) => async token =>
    {
        var context = ScopedServices.GetRequiredService<CosmosProbeDbContext>();
        context.SideEffects.Add(new CosmosSideEffect { Id = Guid.NewGuid().ToString(), Key = key });
        await context.SaveChangesAsync(token);
    };
}
