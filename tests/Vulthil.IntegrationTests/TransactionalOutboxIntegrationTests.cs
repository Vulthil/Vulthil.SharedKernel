using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.SharedKernel.Outbox;
using Vulthil.TestHost.Data;
using Vulthil.TestHost.Probes;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

public sealed class TransactionalOutboxIntegrationTests(TestHarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<TestHarnessWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<TestHarnessWebApplicationFactory>
{
    private ITestHarness Harness => Factory.Services.GetRequiredService<ITestHarness>();

    [Fact]
    public async Task PublishInsideATransactionIsCapturedIntoTheOutboxAndNotSentDirectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dbContext = ScopedServices.GetRequiredService<TestHostDbContext>();
        var outbox = ScopedServices.GetRequiredService<ISaveOutboxMessages>();
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        var capturedDuringTransaction = 0;
        var sentDuringTransaction = false;

        // Act — publish while a business transaction is open.
        await dbContext.ExecuteInTransactionAsync(async cancellationToken =>
        {
            await publisher.PublishAsync(new ProbeCreatedIntegrationEvent(id), cancellationToken);

            // Still inside the open transaction: the message was captured (flushed) into the outbox, and the broker
            // has not seen it. Asserting here is deterministic — the prompt relay cannot observe uncommitted rows.
            capturedDuringTransaction = await outbox.OutboxMessages
                .CountAsync(message => message.Destination == OutboxDestination.Publish, cancellationToken);
            sentDuringTransaction = Harness.Published<ProbeCreatedIntegrationEvent>().Any(message => message.Message.Id == id);

            return 0;
        }, CancellationToken);

        // Assert
        capturedDuringTransaction.ShouldBeGreaterThan(0);
        sentDuringTransaction.ShouldBeFalse();
    }
}
