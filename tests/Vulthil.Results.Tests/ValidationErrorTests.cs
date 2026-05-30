using Vulthil.xUnit;

namespace Vulthil.Results.Tests;

public sealed class ValidationErrorTests : BaseUnitTestCase
{
    [Fact]
    public void WithSingleErrorCreatesValidationError()
    {
        // Arrange
        var error = Error.Failure("Test", "Test error");

        // Act
        var validationError = new ValidationError([error]);

        // Assert
        Assert.Equal("Validation.General", validationError.Code);
        Assert.Equal("One or more validation errors occurred", validationError.Description);
        Assert.Equal(ErrorType.Validation, validationError.Type);
        Assert.Collection(validationError.Errors, 
            e => Assert.Equal(error, e));
    }

    [Fact]
    public void FromResultsWithMultipleResultsCollectsErrors()
    {
        // Arrange
        var error1 = Error.Failure("Test1", "Test error 1");
        var error2 = Error.Failure("Test2", "Test error 2");
        var results = new List<Result> 
        {
            Result.Success(),
            Result.Failure(error1),
            Result.Failure(error2)
        };

        // Act
        var validationError = ValidationError.FromResults(results);

        // Assert
        Assert.Collection(validationError.Errors,
            e => Assert.Equal(error1, e),
            e => Assert.Equal(error2, e));
    }
}
