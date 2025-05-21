using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Tests.Core.Errors;
public sealed class ErrorTests : BaseUnitTestCase
{
    [Fact]
    public void ErrorNoneShouldBeEmptyFailure()
    {
        // Arrange
        var error = Error.None;

        // Act

        // Assert
        error.Code.ShouldBe(string.Empty);
        error.Description.ShouldBe(string.Empty);
        error.Type.ShouldBe(ErrorType.Failure);
    }

    [Fact]
    public void ErrorNullValueShouldBeFailure()
    {
        // Arrange
        var error = Error.NullValue;

        // Act

        // Assert
        error.Code.ShouldBe("General.NullValue");
        error.Description.ShouldBe("Null value was provided");
        error.Type.ShouldBe(ErrorType.Failure);
    }

    public static TheoryData<Error, (string Code, string Description), ErrorType> ErrorTestData => new()
    {
        { Error.NotFound("C", "D"), ("C", "D"), ErrorType.NotFound },
        { Error.Problem("C", "D"), ("C", "D"), ErrorType.Problem },
        { Error.Conflict("C", "D"), ("C", "D"), ErrorType.Conflict },
        { Error.Failure("C", "D"), ("C", "D"), ErrorType.Failure },
        { new ValidationError([Error.NullValue]), ("Validation.General", "One or more validation errors occurred"), ErrorType.Validation },
    };

    [Theory]
    [MemberData(nameof(ErrorTestData))]
    public void ErrorStaticFactoryMethodsShouldCreateErrors(Error error, (string Code, string Description) errorProperties, ErrorType expectedErrorType) =>
        // Assert
        error.ShouldSatisfyAllConditions(e => e.Type.ShouldBe(expectedErrorType), e => e.Code.ShouldBe(errorProperties.Code), e => e.Description.ShouldBe(errorProperties.Description));

    [Fact]
    public void ValidationErrorFromResults()
    {
        // Arrange
        List<Result> results = [Result.Success(), Result.Failure(Error.NullValue)];

        // Act
        var validationError = ValidationError.FromResults(results);

        validationError.Code.ShouldBe("Validation.General");
        validationError.Description.ShouldBe("One or more validation errors occurred");
        validationError.Type.ShouldBe(ErrorType.Validation);
        validationError.Errors.ShouldBe([Error.NullValue]);
    }
}
