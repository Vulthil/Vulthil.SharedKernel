using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.xUnit;
using WebApi.Application;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.SideEffects.Create;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class TransactionalOutboxIntegrationTests(TestHarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<TestHarnessWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<TestHarnessWebApplicationFactory>
{
    private ITestHarness Harness => Factory.Services.GetRequiredService<ITestHarness>();

    [Fact]
    public async Task PublishInsideATransactionIsCapturedIntoTheOutboxAndNotSentDirectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dbContext = ScopedServices.GetRequiredService<IWebApiDbContext>();
        var outbox = ScopedServices.GetRequiredService<ISaveOutboxMessages>();
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        var capturedDuringTransaction = 0;
        var sentDuringTransaction = false;

        // Act — publish while a business transaction is open.
        await dbContext.ExecuteInTransactionAsync(async cancellationToken =>
        {
            await publisher.PublishAsync(new MainEntityCreatedIntegrationEvent(id), cancellationToken);

            // Still inside the open transaction: the message was captured (flushed) into the outbox, and the broker
            // has not seen it. Asserting here is deterministic — the prompt relay cannot observe uncommitted rows.
            capturedDuringTransaction = await outbox.OutboxMessages
                .CountAsync(message => message.Destination == OutboxDestination.Publish, cancellationToken);
            sentDuringTransaction = Harness.Published<MainEntityCreatedIntegrationEvent>().Any(message => message.Message.Id == id);

            return 0;
        }, CancellationToken);

        // Assert
        capturedDuringTransaction.ShouldBeGreaterThan(0);
        sentDuringTransaction.ShouldBeFalse();
    }
}
