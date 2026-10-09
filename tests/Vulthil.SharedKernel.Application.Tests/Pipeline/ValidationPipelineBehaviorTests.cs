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

public sealed class ValidationPipelineBehaviorWithSeveralValidatorsTests : BaseUnitTestCase
{
    private readonly Lazy<ValidationPipelineBehavior<TestCommand, Result>> _lazyTarget;
    private ValidationPipelineBehavior<TestCommand, Result> Target => _lazyTarget.Value;

    public ValidationPipelineBehaviorWithSeveralValidatorsTests()
    {
        _lazyTarget = new(CreateInstance<ValidationPipelineBehavior<TestCommand, Result>>);
    }

    [Fact]
    public async Task EveryValidatorRunsAndEachFailureIsReturnedOnce()
    {
        // Arrange
        Use<IEnumerable<IValidator<TestCommand>>>([new NameRequiredValidator(), new NameLongEnoughValidator()]);
        var request = new TestCommand { Name = string.Empty };
        PipelineDelegate<Result> next = _ => Task.FromResult(Result.Success());

        // Act
        var result = await Target.HandleAsync(request, next, CancellationToken);

        // Assert
        var validationError = Assert.IsType<ValidationError>(result.Error);
        Assert.Collection(
            validationError.Errors,
            error => Assert.Equal("Name.Required", error.Code),
            error => Assert.Equal("Name.TooShort", error.Code));
    }

    [Fact]
    public async Task ValidatorsThatShareAServiceRunOneAfterAnother()
    {
        // Arrange
        var sharedService = new SingleUseService();
        Use<IEnumerable<IValidator<TestCommand>>>([new SharedServiceValidator(sharedService), new SharedServiceValidator(sharedService)]);
        var request = new TestCommand { Name = "Test" };
        var called = false;
        PipelineDelegate<Result> next = _ =>
        {
            called = true;
            return Task.FromResult(Result.Success());
        };

        // Act
        var result = await Target.HandleAsync(request, next, CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(called);
        Assert.Equal(2, sharedService.Uses);
        Assert.Equal(1, sharedService.MostUsesAtOnce);
    }

    public sealed class NameRequiredValidator : AbstractValidator<TestCommand>
    {
        public NameRequiredValidator() => RuleFor(command => command.Name).NotEmpty().WithErrorCode("Name.Required");
    }

    public sealed class NameLongEnoughValidator : AbstractValidator<TestCommand>
    {
        public NameLongEnoughValidator() => RuleFor(command => command.Name).MinimumLength(3).WithErrorCode("Name.TooShort");
    }

    public sealed class SharedServiceValidator : AbstractValidator<TestCommand>
    {
        public SharedServiceValidator(SingleUseService service) =>
            RuleFor(command => command.Name).MustAsync((_, cancellationToken) => service.CheckAsync(cancellationToken));
    }

    public sealed class SingleUseService
    {
        private readonly Lock _gate = new();
        private int _usesNow;

        public int Uses { get; private set; }

        public int MostUsesAtOnce { get; private set; }

        public async Task<bool> CheckAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                Uses++;
                _usesNow++;
                MostUsesAtOnce = Math.Max(MostUsesAtOnce, _usesNow);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

            lock (_gate)
            {
                _usesNow--;
            }

            return true;
        }
    }
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
    public async Task WithInvalidRequestThrowsAValidationExceptionCarryingTheFailuresAsAValidationError()
    {
        // Arrange
        var request = new TestCommandWithPlainResponse();
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommandWithPlainResponse>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult(new List<ValidationFailure>
                    {
                        new ValidationFailure("Name", "Name is required") { ErrorCode = "NotEmptyValidator" }
                    }));
        PipelineDelegate<string> next = _ => Task.FromResult("unused");

        // Act
        var exception = await Assert.ThrowsAsync<CommandValidationException>(() => Target.HandleAsync(request, next, CancellationToken));

        // Assert
        Assert.IsAssignableFrom<ValidationException>(exception);
        Assert.Same(exception.Error, ((IHasError)exception).Error);
        var failure = Assert.Single(exception.Error.Errors);
        Assert.Equal("NotEmptyValidator", failure.Code);
        Assert.Equal("Name is required", failure.Description);
        Assert.Equal(ErrorType.Validation, failure.Type);
    }
}

public class TestCommandWithPlainResponse : ICommand<string>;
