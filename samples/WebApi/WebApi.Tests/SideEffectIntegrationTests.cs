using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.SideEffects;
using WebApi.Application.SideEffects.GetInProgress;
using WebApi.Domain.SideEffects;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

/// <summary>
/// Represents the SideEffectIntegrationTests.
/// </summary>
public sealed class SideEffectIntegrationTests(FixtureWrapper testFixture, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase(testFixture, testOutputHelper)
{
    /// <summary>
    /// Executes this member.
    /// </summary>
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
}


