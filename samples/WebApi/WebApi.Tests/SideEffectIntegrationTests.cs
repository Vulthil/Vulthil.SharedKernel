using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.GetInProgress;
using WebApi.Domain.SideEffects;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public sealed class SideEffectIntegrationTests(FixtureWrapper testFixture, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase(testFixture, testOutputHelper)
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
        });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldContain(s =>
            s.MainEntityId == createResult.Value && s.Status is Status.InProgressStatus);
    }
}


