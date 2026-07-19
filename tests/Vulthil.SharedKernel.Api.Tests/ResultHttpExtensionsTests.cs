using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Api.Tests;

public sealed class ResultHttpExtensionsTests : BaseUnitTestCase
{
    public static TheoryData<Error, int> NonValidationErrors => new()
    {
        { Error.NotFound("Entity.NotFound", "Entity was not found"), StatusCodes.Status404NotFound },
        { Error.Conflict("Entity.Conflict", "Entity already exists"), StatusCodes.Status409Conflict },
        { Error.Problem("Entity.Problem", "Entity is in a bad state"), StatusCodes.Status400BadRequest },
        { Error.Failure("Entity.Failure", "Something went wrong"), StatusCodes.Status500InternalServerError },
    };

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public void ErrorToIResultReturnsProblemDetailsWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange

        // Act
        var problemResult = error.ToIResult();

        // Assert
        Assert.Equal(expectedStatusCode, problemResult.StatusCode);
        Assert.Equal(expectedStatusCode, problemResult.ProblemDetails.Status);
        Assert.Equal(error.Description, problemResult.ProblemDetails.Detail);
        Assert.Contains(error.Code, problemResult.ProblemDetails.Extensions.Keys);
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public void ResultToIResultOnFailureReturnsProblemDetailsWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange
        var result = Result.Failure(error);

        // Act
        var httpResult = result.ToIResult();

        // Assert
        var problemResult = Assert.IsType<ProblemHttpResult>(httpResult.Result);
        Assert.Equal(expectedStatusCode, problemResult.StatusCode);
        Assert.Equal(error.Description, problemResult.ProblemDetails.Detail);
        Assert.Contains(error.Code, problemResult.ProblemDetails.Extensions.Keys);
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public void ResultOfTToIResultOnFailureReturnsProblemDetailsWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange
        var result = Result.Failure<string>(error);

        // Act
        var httpResult = result.ToIResult();

        // Assert
        var problemResult = Assert.IsType<ProblemHttpResult>(httpResult.Result);
        Assert.Equal(expectedStatusCode, problemResult.StatusCode);
        Assert.Equal(error.Description, problemResult.ProblemDetails.Detail);
        Assert.Contains(error.Code, problemResult.ProblemDetails.Extensions.Keys);
    }

    [Fact]
    public void ResultOfTToIResultOnValidationFailureReturnsValidationProblemWithFieldErrors()
    {
        // Arrange
        var validationError = new ValidationError([Error.Validation("Entity.Field", "Field is required")]);
        var result = Result.Failure<string>(validationError);

        // Act
        var httpResult = result.ToIResult();

        // Assert
        var validationProblem = Assert.IsType<ValidationProblem>(httpResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, validationProblem.StatusCode);
        Assert.Contains("Entity.Field", validationProblem.ProblemDetails.Errors.Keys);
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public void ErrorToActionResultReturnsProblemObjectResultWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange
        var controller = CreateController();

        // Act
        var actionResult = error.ToActionResult(controller);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(error.Description, problemDetails.Detail);
        Assert.Contains(error.Code, problemDetails.Extensions.Keys);
    }

    [Fact]
    public void ErrorToActionResultOnValidationErrorReturnsValidationProblemWithFieldErrors()
    {
        // Arrange
        var controller = CreateController();
        var validationError = new ValidationError([Error.Validation("Entity.Field", "Field is required")]);

        // Act
        var actionResult = validationError.ToActionResult(controller);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains("Entity.Field", problemDetails.Errors.Keys);
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public void ToIResultAndToActionResultProduceTheSameStatusCodeAndDetailForTheSameError(Error error, int expectedStatusCode)
    {
        // Arrange
        var controller = CreateController();

        // Act
        var httpResult = error.ToIResult();
        var actionResult = Assert.IsType<ObjectResult>(error.ToActionResult(controller));
        var actionProblemDetails = Assert.IsType<ProblemDetails>(actionResult.Value);

        // Assert
        Assert.Equal(expectedStatusCode, httpResult.StatusCode);
        Assert.Equal(actionResult.StatusCode, httpResult.StatusCode);
        Assert.Equal(actionProblemDetails.Detail, httpResult.ProblemDetails.Detail);
    }

    private static TestController CreateController()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        var provider = services.BuildServiceProvider();

        return new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = provider }
            }
        };
    }

    private sealed class TestController : ControllerBase;
}
