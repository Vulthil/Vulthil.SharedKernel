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
        Assert.Contains(validationError.Errors, e => e.Code == "Name" && e.Description == "Name is required");
    }
}

public class TestCommand : ICommand<Result>
{
    public string Name { get; set; } = string.Empty;
}
