using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Extensions.Testing;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.TestHarness;
using Vulthil.Results;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.TestHost.Data;
using Vulthil.TestHost.Probes;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

public sealed class TransactionalOutboxIntegrationTests(TestHarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<TestHarnessWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<TestHarnessWebApplicationFactory>
{
    private ITestHarness Harness => Factory.Services.GetRequiredService<ITestHarness>();
    private IRequester Requester => Factory.Services.GetRequiredService<IRequester>();

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

            capturedDuringTransaction = await outbox.OutboxMessages
                .CountAsync(message => message.Destination == OutboxDestination.Publish, cancellationToken);
            sentDuringTransaction = Harness.Published<ProbeCreatedIntegrationEvent>().Any(message => message.Message.Id == id);

            return 0;
        }, CancellationToken);

        // Assert
        capturedDuringTransaction.ShouldBeGreaterThan(0);
        sentDuringTransaction.ShouldBeFalse();
    }

    [Fact]
    public async Task CommittingATransactionRelaysTheMessageAndTheConsumerReceivesItWithTheStableMessageId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var messageId = Guid.NewGuid().ToString();
        var dbContext = ScopedServices.GetRequiredService<TestHostDbContext>();
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        // Act — publish inside a committed transaction, then wait for the real relay to dispatch it.
        await dbContext.ExecuteInTransactionAsync(async cancellationToken =>
        {
            await publisher.PublishAsync(
                new ProbeCreatedIntegrationEvent(id),
                context =>
                {
                    context.SetMessageId(messageId);
                    return ValueTask.CompletedTask;
                },
                cancellationToken);

            return 0;
        }, CancellationToken);

        var polled = await Polling.WaitAsync(TimeSpan.FromSeconds(15), async () =>
        {
            var consumed = Harness.Consumed<ProbeCreatedIntegrationEvent>().FirstOrDefault(message => message.Message.Id == id);
            return consumed is not null
                ? Result.Success(consumed)
                : Result.Failure<CapturedMessage<ProbeCreatedIntegrationEvent>>(
                    Error.NotFound("Probe.NotYetRelayed", "The captured message has not been relayed and consumed yet."));
        }, CancellationToken);

        // Assert
        polled.IsSuccess.ShouldBeTrue();
        polled.Value.Envelope.MessageId.ShouldBe(messageId);

        var sideEffects = await Requester.RequestAsync<GetProbeSideEffects, List<ProbeSideEffectDto>>(new GetProbeSideEffects(id), CancellationToken);
        sideEffects.IsSuccess.ShouldBeTrue();
        sideEffects.Value.ShouldContain(sideEffect => sideEffect.ProbeId == id);
    }

    [Fact]
    public async Task RollingBackATransactionNeverRelaysTheCapturedMessage()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dbContext = ScopedServices.GetRequiredService<TestHostDbContext>();
        var publisher = ScopedServices.GetRequiredService<IPublisher>();

        // Act — the operation reports failure, so ExecuteInTransactionAsync rolls back instead of committing.
        await dbContext.ExecuteInTransactionAsync(
            async cancellationToken =>
            {
                await publisher.PublishAsync(new ProbeCreatedIntegrationEvent(id), cancellationToken);
                return false;
            },
            static operationSucceeded => operationSucceeded,
            CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken);

        // Assert — fetch every row and filter in memory: Content is mapped to jsonb, which Npgsql cannot LIKE-match server-side.
        var outboxMessages = await dbContext.OutboxMessages.AsNoTracking().ToListAsync(CancellationToken);
        outboxMessages.ShouldNotContain(message => message.Content.Contains(id.ToString()));
        Harness.Published<ProbeCreatedIntegrationEvent>().ShouldNotContain(message => message.Message.Id == id);
        Harness.Consumed<ProbeCreatedIntegrationEvent>().ShouldNotContain(message => message.Message.Id == id);
    }
}
