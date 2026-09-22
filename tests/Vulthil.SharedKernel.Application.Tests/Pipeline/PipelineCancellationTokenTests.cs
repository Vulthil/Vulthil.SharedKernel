using Microsoft.Extensions.DependencyInjection;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests.Pipeline;

/// <summary>
/// Pins the cancellation contract of the request pipeline: the token a caller hands to <see cref="ISender"/> is the
/// token every behavior receives and, when a behavior passes it on, the token the handler receives — nothing along
/// the chain substitutes a fresh one.
/// </summary>
public sealed class PipelineCancellationTokenTests : BaseUnitTestCase
{
    [Fact]
    public async Task TheCallersTokenReachesTheBehaviorAndTheHandlerUnchanged()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<TokenProbe>();
        services.AddApplication(options =>
        {
            options.RegisterHandlerAssemblies(typeof(PipelineCancellationTokenTests).Assembly);
            options.AddOpenPipelineHandler(typeof(TokenRecordingBehavior<,>));
        });
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var probe = scope.ServiceProvider.GetRequiredService<TokenProbe>();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new TokenProbeCommand(), cancellation.Token);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        probe.BehaviorToken.ShouldBe(cancellation.Token);
        probe.HandlerToken.ShouldBe(cancellation.Token);
    }

    public sealed class TokenProbe
    {
        public CancellationToken? BehaviorToken { get; set; }

        public CancellationToken? HandlerToken { get; set; }
    }

    public sealed record TokenProbeCommand : ICommand;

    public sealed class TokenProbeCommandHandler(TokenProbe probe) : ICommandHandler<TokenProbeCommand>
    {
        public Task<Result> HandleAsync(TokenProbeCommand request, CancellationToken cancellationToken = default)
        {
            probe.HandlerToken = cancellationToken;
            return Task.FromResult(Result.Success());
        }
    }

    public sealed class TokenRecordingBehavior<TRequest, TResponse>(TokenProbe probe) : IPipelineHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
        {
            probe.BehaviorToken = cancellationToken;
            return next(cancellationToken);
        }
    }
}
