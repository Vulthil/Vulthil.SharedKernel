using FluentValidation;
using FluentValidation.Results;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Behaviors;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Application.Tests.Pipeline;

public sealed class ValidationPipelineBehaviorTests : BaseUnitTestCase
{
    private readonly Lazy<ValidationPipelineBehavior<TestCommand, Result>> _lazyTarget;
    private readonly Mock<IValidator<TestCommand>> _validatorMock;
    private ValidationPipelineBehavior<TestCommand, Result> Target => _lazyTarget.Value;

    public ValidationPipelineBehaviorTests()
    {
        _validatorMock = GetMock<IValidator<TestCommand>>();
        Use<IEnumerable<IValidator>>([_validatorMock.Object]);
        _lazyTarget = new(CreateInstance<ValidationPipelineBehavior<TestCommand, Result>>);
    }

    [Fact]
    public async Task WithValidRequestCallsNextDelegate()
    {
        // Arrange
        var request = new TestCommand { Name = "Test" };
        var expectedResult = Result.Success();
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult(new List<ValidationFailure>()));
        var called = false;
        PipelineDelegate<Result> next = _ =>
        {
            called = true;
            return Task.FromResult(expectedResult);
        };

        // Act
        var result = await Target.HandleAsync(request, next, CancellationToken);

        // Assert
        Assert.True(called);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task WithInvalidRequestReturnsValidationError()
    {
        // Arrange
        var request = new TestCommand { Name = string.Empty };
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult(new List<ValidationFailure>
                    {
                        new ValidationFailure("Name", "Name is required")
                        {
                            ErrorCode = "Name"
                        }
                    }));
        PipelineDelegate<Result> next = _ => Task.FromResult(Result.Success());

        // Act
        var result = await Target.HandleAsync(request, next, CancellationToken);

        // Assert
        Assert.True(result.IsFailure);
        var validationError = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(validationError.Errors, e => e.Code == "Name" && e.Description == "Name is required" && e.Type == ErrorType.Validation);
    }
}

public class TestCommand : ICommand<Result>
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ValidationPipelineBehaviorWithResultOfTResponseTests : BaseUnitTestCase
{
    private readonly Lazy<ValidationPipelineBehavior<TestCommandWithValue, Result<string>>> _lazyTarget;
    private readonly Mock<IValidator<TestCommandWithValue>> _validatorMock;
    private ValidationPipelineBehavior<TestCommandWithValue, Result<string>> Target => _lazyTarget.Value;

    public ValidationPipelineBehaviorWithResultOfTResponseTests()
    {
        _validatorMock = GetMock<IValidator<TestCommandWithValue>>();
        Use<IEnumerable<IValidator>>([_validatorMock.Object]);
        _lazyTarget = new(CreateInstance<ValidationPipelineBehavior<TestCommandWithValue, Result<string>>>);
    }

    [Fact]
    public async Task WithValidRequestCallsNextDelegate()
    {
        // Arrange
        var request = new TestCommandWithValue { Name = "Test" };
        var expectedResult = Result.Success("Test");
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommandWithValue>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult(new List<ValidationFailure>()));
        PipelineDelegate<Result<string>> next = _ => Task.FromResult(expectedResult);

        // Act
        var result = await Target.HandleAsync(request, next, CancellationToken);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task WithInvalidRequestReturnsFailedResultCarryingTheValidationError()
    {
        // Arrange
        var request = new TestCommandWithValue { Name = string.Empty };
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommandWithValue>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult(new List<ValidationFailure>
                    {
                        new ValidationFailure("Name", "Name is required")
                        {
                            ErrorCode = "Name"
                        }
                    }));
        PipelineDelegate<Result<string>> next = _ => Task.FromResult(Result.Success("unused"));

        // Act
        var result = await Target.HandleAsync(request, next, CancellationToken);

        // Assert
        Assert.True(result.IsFailure);
        var validationError = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(validationError.Errors, e => e.Code == "Name" && e.Description == "Name is required" && e.Type == ErrorType.Validation);
    }
}

public class TestCommandWithValue : ICommand<Result<string>>
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ValidationPipelineBehaviorWithNonResultResponseTests : BaseUnitTestCase
{
    private readonly Lazy<ValidationPipelineBehavior<TestCommandWithPlainResponse, string>> _lazyTarget;
    private readonly Mock<IValidator<TestCommandWithPlainResponse>> _validatorMock;
    private ValidationPipelineBehavior<TestCommandWithPlainResponse, string> Target => _lazyTarget.Value;

    public ValidationPipelineBehaviorWithNonResultResponseTests()
    {
        _validatorMock = GetMock<IValidator<TestCommandWithPlainResponse>>();
        Use<IEnumerable<IValidator>>([_validatorMock.Object]);
        _lazyTarget = new(CreateInstance<ValidationPipelineBehavior<TestCommandWithPlainResponse, string>>);
    }

    [Fact]
    public async Task WithInvalidRequestThrowsValidationException()
    {
        // Arrange
        var request = new TestCommandWithPlainResponse();
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommandWithPlainResponse>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult(new List<ValidationFailure>
                    {
                        new ValidationFailure("Name", "Name is required")
                    }));
        PipelineDelegate<string> next = _ => Task.FromResult("unused");

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => Target.HandleAsync(request, next, CancellationToken));
    }
}

public class TestCommandWithPlainResponse : ICommand<string>;
