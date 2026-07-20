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
    public void CustomResultsProblemReturnsProblemHttpResultWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange

        // Act
        var problemResult = CustomResults.Problem(error);

        // Assert
        Assert.Equal(expectedStatusCode, problemResult.StatusCode);
        Assert.Equal(error.Description, problemResult.ProblemDetails.Detail);
        Assert.Contains(error.Code, problemResult.ProblemDetails.Extensions.Keys);
    }

    [Fact]
    public void CustomResultsProblemThrowsOnNullError()
    {
        // Arrange

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CustomResults.Problem(null!));
    }

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

    [Fact]
    public void ResultToActionResultOnSuccessReturnsNoContent()
    {
        // Arrange
        var controller = CreateController();
        var result = Result.Success();

        // Act
        var actionResult = result.ToActionResult(controller);

        // Assert
        Assert.IsType<NoContentResult>(actionResult);
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public void ResultToActionResultOnFailureReturnsProblemObjectResultWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange
        var controller = CreateController();
        var result = Result.Failure(error);

        // Act
        var actionResult = result.ToActionResult(controller);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(error.Description, problemDetails.Detail);
    }

    [Fact]
    public void ResultOfTToActionResultOnSuccessReturnsOkWithValue()
    {
        // Arrange
        var controller = CreateController();
        var result = Result.Success("value");

        // Act
        var actionResult = result.ToActionResult(controller);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal("value", okResult.Value);
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public void ResultOfTToActionResultOnFailureReturnsProblemObjectResultWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange
        var controller = CreateController();
        var result = Result.Failure<string>(error);

        // Act
        var actionResult = result.ToActionResult(controller);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
    }

    [Fact]
    public void ResultToIResultOnSuccessReturnsNoContent()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var httpResult = result.ToIResult();

        // Assert
        Assert.IsType<NoContent>(httpResult.Result);
    }

    [Fact]
    public void ResultOfTToIResultOnSuccessReturnsOkWithValue()
    {
        // Arrange
        var result = Result.Success("value");

        // Act
        var httpResult = result.ToIResult();

        // Assert
        var ok = Assert.IsType<Ok<string>>(httpResult.Result);
        Assert.Equal("value", ok.Value);
    }

    [Fact]
    public async Task ToActionResultAsyncOnSuccessReturnsNoContent()
    {
        // Arrange
        var controller = CreateController();
        var resultTask = Task.FromResult(Result.Success());

        // Act
        var actionResult = await resultTask.ToActionResultAsync(controller);

        // Assert
        Assert.IsType<NoContentResult>(actionResult);
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public async Task ToActionResultAsyncOnFailureReturnsProblemObjectResultWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange
        var controller = CreateController();
        var resultTask = Task.FromResult(Result.Failure(error));

        // Act
        var actionResult = await resultTask.ToActionResultAsync(controller);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
    }

    [Fact]
    public async Task ToActionResultAsyncThrowsOnNullTask()
    {
        // Arrange
        var controller = CreateController();
        Task<Result> resultTask = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => resultTask.ToActionResultAsync(controller));
    }

    [Fact]
    public async Task ToActionResultAsyncThrowsOnNullController()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => resultTask.ToActionResultAsync(null!));
    }

    [Fact]
    public async Task ToActionResultAsyncOfTOnSuccessReturnsOkWithValue()
    {
        // Arrange
        var controller = CreateController();
        var resultTask = Task.FromResult(Result.Success("value"));

        // Act
        var actionResult = await resultTask.ToActionResultAsync(controller);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal("value", okResult.Value);
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public async Task ToActionResultAsyncOfTOnFailureReturnsProblemObjectResultWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange
        var controller = CreateController();
        var resultTask = Task.FromResult(Result.Failure<string>(error));

        // Act
        var actionResult = await resultTask.ToActionResultAsync(controller);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
    }

    [Fact]
    public void ToCreatedAtRouteHttpResultOnSuccessReturnsCreatedAtRouteWithRouteNameAndRouteValues()
    {
        // Arrange
        var result = Result.Success("created-value");

        // Act
        var httpResult = result.ToCreatedAtRouteHttpResult("GetThing", value => new { id = value });

        // Assert
        var createdAtRoute = Assert.IsType<CreatedAtRoute<string>>(httpResult.Result);
        Assert.Equal("GetThing", createdAtRoute.RouteName);
        Assert.Equal("created-value", createdAtRoute.Value);
        Assert.Equal(StatusCodes.Status201Created, createdAtRoute.StatusCode);
        Assert.NotNull(createdAtRoute.RouteValues);
    }

    [Fact]
    public void ToCreatedAtRouteHttpResultOnSuccessWithoutRouteNameOrValueFactoryUsesNullRouteNameAndEmptyRouteValues()
    {
        // Arrange
        var result = Result.Success("created-value");

        // Act
        var httpResult = result.ToCreatedAtRouteHttpResult();

        // Assert
        var createdAtRoute = Assert.IsType<CreatedAtRoute<string>>(httpResult.Result);
        Assert.Null(createdAtRoute.RouteName);
        Assert.True(createdAtRoute.RouteValues is null or { Count: 0 });
    }

    [Theory]
    [MemberData(nameof(NonValidationErrors))]
    public void ToCreatedAtRouteHttpResultOnFailureReturnsProblemDetailsWithCodeAndDescription(Error error, int expectedStatusCode)
    {
        // Arrange
        var result = Result.Failure<string>(error);

        // Act
        var httpResult = result.ToCreatedAtRouteHttpResult("GetThing");

        // Assert
        var problemResult = Assert.IsType<ProblemHttpResult>(httpResult.Result);
        Assert.Equal(expectedStatusCode, problemResult.StatusCode);
        Assert.Equal(error.Description, problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public void ToCreatedAtRouteHttpResultOnValidationFailureReturnsValidationProblemWithFieldErrors()
    {
        // Arrange
        var validationError = new ValidationError([Error.Validation("Entity.Field", "Field is required")]);
        var result = Result.Failure<string>(validationError);

        // Act
        var httpResult = result.ToCreatedAtRouteHttpResult("GetThing");

        // Assert
        var validationProblem = Assert.IsType<ValidationProblem>(httpResult.Result);
        Assert.Contains("Entity.Field", validationProblem.ProblemDetails.Errors.Keys);
    }

    [Fact]
    public void ToCreatedAtRouteHttpResultThrowsOnNullResult()
    {
        // Arrange
        Result<string> result = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => result.ToCreatedAtRouteHttpResult());
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
