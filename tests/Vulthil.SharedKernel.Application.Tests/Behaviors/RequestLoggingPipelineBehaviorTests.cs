using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Behaviors;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests.Behaviors;

public sealed class RequestLoggingPipelineBehaviorTests : BaseUnitTestCase
{
    private readonly Lazy<LoggingBehaviors.RequestLoggingPipelineBehavior<TestRequest, Result>> _lazyTarget;
    private readonly ScopeCapturingLogger<LoggingBehaviors.RequestLoggingPipelineBehavior<TestRequest, Result>> _logger;
    private LoggingBehaviors.RequestLoggingPipelineBehavior<TestRequest, Result> Target => _lazyTarget.Value;

    public RequestLoggingPipelineBehaviorTests()
    {
        _logger = new();
        Use<ILogger<LoggingBehaviors.RequestLoggingPipelineBehavior<TestRequest, Result>>>(_logger);
        _lazyTarget = new(CreateInstance<LoggingBehaviors.RequestLoggingPipelineBehavior<TestRequest, Result>>);
    }

    [Fact]
    public async Task SuccessfulResultLogsNoErrorScope()
    {
        // Arrange
        PipelineDelegate<Result> next = _ => Task.FromResult(Result.Success());

        // Act
        await Target.HandleAsync(new TestRequest(), next, CancellationToken);

        // Assert
        Assert.Empty(_logger.Scopes);
    }

    [Fact]
    public async Task FailedResultWithValidationErrorLogsScopeContainingInnerErrorDetails()
    {
        // Arrange
        var validationError = new ValidationError([Error.Validation("Name", "Name is required")]);
        PipelineDelegate<Result> next = _ => Task.FromResult(Result.Failure(validationError));

        // Act
        await Target.HandleAsync(new TestRequest(), next, CancellationToken);

        // Assert
        var scope = Assert.IsType<Dictionary<string, string>>(Assert.Single(_logger.Scopes));
        Assert.Contains("Name", scope["Error"], StringComparison.Ordinal);
        Assert.Contains("Name is required", scope["Error"], StringComparison.Ordinal);
    }

    public sealed record TestRequest : IRequest<Result>;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class ScopeCapturingLogger<T> : ILogger<T>
    {
        public List<object> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            Scopes.Add(state);
            return NoopDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
