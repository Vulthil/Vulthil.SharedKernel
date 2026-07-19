using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Provides extension methods for converting <see cref="Result"/> values to HTTP responses.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>
    /// Converts a <see cref="Task{Result}"/> to an <see cref="IActionResult"/>, returning 204 No Content on success.
    /// </summary>
    public static async Task<IActionResult> ToActionResultAsync(this Task<Result> resultTask, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(controller);

        var result = await resultTask;
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return result.Error.ToActionResult(controller);
    }

    /// <summary>
    /// Converts a <see cref="Task{Result}"/> to an <see cref="IActionResult"/>, returning 200 OK with the value on success.
    /// </summary>
    public static async Task<IActionResult> ToActionResultAsync<T>(this Task<Result<T>> resultTask, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(controller);

        var result = await resultTask;
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return result.Error.ToActionResult(controller);
    }

    /// <summary>
    /// Converts an <see cref="Error"/> to an appropriate <see cref="IActionResult"/> based on the error type, producing
    /// an RFC 7807 <see cref="ProblemDetails"/> body carrying the error's code and description for every non-validation
    /// error type.
    /// </summary>
    public static IActionResult ToActionResult(this Error error, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(controller);

        if (error.Type != ErrorType.Validation)
        {
            return ProblemActionResult(error);
        }

        if (error is ValidationError validationError)
        {
            foreach (var innerError in validationError.Errors)
            {
                controller.ModelState.AddModelError(innerError.Code, innerError.Description);
            }
        }
        else
        {
            controller.ModelState.AddModelError(error.Code, error.Description);
        }

        return controller.ValidationProblem();
    }

    /// <summary>
    /// Converts a <see cref="Result{T}"/> to an <see cref="IActionResult"/>, returning 200 OK with the value on success.
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return result.Error.ToActionResult(controller);
    }


    /// <summary>
    /// Converts a <see cref="Result"/> to an <see cref="IActionResult"/>, returning 204 No Content on success.
    /// </summary>
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return result.Error.ToActionResult(controller);
    }

    /// <summary>
    /// Converts a <see cref="Result{T}"/> to a typed <see cref="IResult"/> for minimal API and controller endpoints,
    /// returning distinct HTTP result types for OpenAPI documentation generation.
    /// </summary>
    public static Results<CreatedAtRoute<T>, ValidationProblem, NotFound, Conflict, ProblemHttpResult> ToCreatedAtRouteHttpResult<T>(this Result<T> result, string? routeName = null, Func<T, object?>? routeValueFactory = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return TypedResults.CreatedAtRoute(result.Value, routeName, routeValueFactory != null ? routeValueFactory(result.Value) : null);
        }

        return MapError<CreatedAtRoute<T>>(result.Error);
    }

    /// <summary>
    /// Converts a <see cref="Result{T}"/> to a typed <see cref="IResult"/> for minimal API and controller endpoints,
    /// returning distinct HTTP result types for OpenAPI documentation generation.
    /// </summary>
    public static Results<Ok<T>, ValidationProblem, NotFound, Conflict, ProblemHttpResult> ToIResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return MapError<Ok<T>>(result.Error);
    }

    /// <summary>
    /// Converts a <see cref="Result"/> to a typed <see cref="IResult"/> for minimal API and controller endpoints,
    /// returning distinct HTTP result types for OpenAPI documentation generation.
    /// </summary>
    public static Results<NoContent, ValidationProblem, NotFound, Conflict, ProblemHttpResult> ToIResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return TypedResults.NoContent();
        }

        return MapError<NoContent>(result.Error);
    }

    /// <summary>
    /// Converts an <see cref="Error"/> to a <see cref="ProblemHttpResult"/> problem response for minimal API endpoints.
    /// </summary>
    public static ProblemHttpResult ToIResult(this Error error) => CustomResults.Problem(error);

    private static ObjectResult ProblemActionResult(Error error)
    {
        var statusCode = CustomResults.GetStatusCode(error.Type);

        var problemDetails = new ProblemDetails
        {
            Detail = error.Description,
            Status = statusCode
        };

        foreach (var entry in CustomResults.GetErrorsDictionary(error))
        {
            problemDetails.Extensions[entry.Key] = entry.Value;
        }

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
    }

    private static Results<TSuccess, ValidationProblem, NotFound, Conflict, ProblemHttpResult> MapError<TSuccess>(Error error)
        where TSuccess : IResult
    {
        if (error.Type != ErrorType.Validation)
        {
            return CustomResults.Problem(error);
        }

        var errors = CustomResults.GetErrorsDictionary(error);

        return TypedResults.ValidationProblem(errors, error.Description);
    }
}

/// <summary>
/// Provides factory methods for creating problem-detail HTTP responses from <see cref="Error"/> values.
/// </summary>
public static class CustomResults
{
    /// <summary>
    /// Creates a <see cref="ProblemHttpResult"/> from an <see cref="Error"/>, mapping the error type to the appropriate HTTP status code.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <returns>A <see cref="ProblemHttpResult"/> representing the problem response.</returns>
    public static ProblemHttpResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var errors = GetErrorsDictionary(error);

        return TypedResults.Problem(
            detail: error.Description,
            statusCode: GetStatusCode(error.Type),
            extensions: errors.ToDictionary(s => s.Key, s => (object?)s.Value));
    }

    /// <summary>
    /// Builds a dictionary of error codes and their descriptions from the given <see cref="Error"/>.
    /// </summary>
    /// <param name="error">The error to extract details from.</param>
    /// <returns>A dictionary mapping error codes to arrays of descriptions.</returns>
    internal static Dictionary<string, string[]> GetErrorsDictionary(Error error) =>
        error is ValidationError validationError
            ? validationError.Errors
                .GroupBy(e => e.Code, s => s.Description)
                .ToDictionary(e => e.Key, errors => errors.ToArray())
            : new Dictionary<string, string[]>
            {
                [error.Code] = [error.Description]
            };

    /// <summary>
    /// Maps an <see cref="ErrorType"/> to the HTTP status code used across both the typed-<see cref="IResult"/> and
    /// <see cref="IActionResult"/> problem-response paths.
    /// </summary>
    /// <param name="errorType">The error classification to map.</param>
    /// <returns>The corresponding HTTP status code.</returns>
    internal static int GetStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Problem => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };
}
