using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.GetInProgress;
using WebApi.Domain.SideEffects;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class SideEffectIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase(factory, testOutputHelper)
{
    [Fact]
    public async Task TestCreate()
    {
        // Arrange
        var command = new CreateMainEntityCommand(Guid.NewGuid().ToString());
        var createResult = await Sender.SendAsync(command, CancellationToken);
        createResult.IsSuccess.ShouldBeTrue();

        var query = new GetInProgressQuery();

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var queryResult = await Sender.SendAsync(query, CancellationToken);

            if (queryResult.IsFailure)
            {
                return Result.Failure<List<SideEffectDto>>(Error.NullValue);
            }

            if (!queryResult.Value.Any(s => s.MainEntityId == createResult.Value))
            {
                return Result.Failure<List<SideEffectDto>>(Error.NotFound("NotFound", "No side effects found"));
            }

            return queryResult;
        }, cancellationToken: CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(s =>
            s.MainEntityId == createResult.Value && s.Status is Status.InProgressStatus);
    }

    [Fact]
    public async Task SideEffect_starts_at_the_time_of_a_TimeProvider_replaced_for_this_test()
    {
        // Arrange
        var now = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        Use<TimeProvider>(new FrozenClock(now));
        var createResult = await Sender.SendAsync(new CreateMainEntityCommand(Guid.NewGuid().ToString()), CancellationToken);

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(10), async () =>
        {
            var queryResult = await Sender.SendAsync(new GetInProgressQuery(), CancellationToken);
            return queryResult.Bind(sideEffects => sideEffects.Find(s => s.MainEntityId == createResult.Value)
                .ToResult(Error.NotFound("NotFound", "No side effect found")));
        }, cancellationToken: CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBeOfType<Status.InProgressStatus>().ProgressTime.ShouldBe(now);
    }

    /// <summary>
    /// Stops the wall clock but keeps real timers, so the outbox relay and the consumers still run on schedule.
    /// </summary>
    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
