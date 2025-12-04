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
    [Fact]
    public async Task WithValidRequestCallsNextDelegate()
    {
        // Arrange
        var request = new TestCommand { Name = "Test" };
        var expectedResult = Result.Success();
        var mockValidator = GetMock<IValidator<TestCommand>>();
        Use<IEnumerable<IValidator>>([mockValidator.Object]);
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult(new List<ValidationFailure>()));
        var target = CreateInstance<ValidationPipelineBehavior<TestCommand, Result>>();
        var called = false;
        PipelineDelegate<Result> next = _ =>
        {
            called = true;
            return Task.FromResult(expectedResult);
        };

        // Act
        var result = await target.HandleAsync(request, next, CancellationToken);

        // Assert
        Assert.True(called);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task WithInvalidRequestReturnsValidationError()
    {
        // Arrange
        var request = new TestCommand { Name = string.Empty };
        var mockValidator = GetMock<IValidator<TestCommand>>();
        Use<IEnumerable<IValidator>>([mockValidator.Object]);
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult(new List<ValidationFailure>
                    {
                        new ValidationFailure("Name", "Name is required")
                        {
                            ErrorCode = "Name"
                        }
                    }));
        var target = CreateInstance<ValidationPipelineBehavior<TestCommand, Result>>();
        PipelineDelegate<Result> next = _ => Task.FromResult(Result.Success());

        // Act
        var result = await target.HandleAsync(request, next, CancellationToken);

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
