using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Provides extension methods for converting <see cref="Result"/> values to HTTP responses.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Converts a <see cref="Result"/> to an <see cref="IActionResult"/>, returning 204 No Content on success.
    /// </summary>
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return result.Error.ToActionResult(controller);
    }

    /// <summary>
    /// Converts a <see cref="Result{T}"/> to an <see cref="IActionResult"/>, returning 200 OK with the value on success.
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return result.Error.ToActionResult(controller);
    }

    /// <summary>
    /// Converts an <see cref="Error"/> to an appropriate <see cref="IActionResult"/> based on the error type.
    /// </summary>
    public static IActionResult ToActionResult(this Error error, ControllerBase controller)
    {
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

        return error.Type switch
        {
            ErrorType.Validation => controller.ValidationProblem(),
            ErrorType.NotFound => controller.NotFound(),
            ErrorType.Conflict => controller.Conflict(),
            _ => controller.Problem()
        };
    }

    /// <summary>
    /// Converts a <see cref="Result{T}"/> to a typed <see cref="IResult"/> for minimal API and controller endpoints,
    /// returning distinct HTTP result types for OpenAPI documentation generation.
    /// </summary>
    public static Results<CreatedAtRoute<T>, ValidationProblem, NotFound, Conflict, ProblemHttpResult> ToCreatedAtRouteHttpResult<T>(this Result<T> result, string? routeName = null, Func<T, object?>? routeValueFactory = null)
    {
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

    private static Results<TSuccess, ValidationProblem, NotFound, Conflict, ProblemHttpResult> MapError<TSuccess>(Error error)
        where TSuccess : IResult
    {
        var errors = CustomResults.GetErrorsDictionary(error);

        return error.Type switch
        {
            ErrorType.Validation => TypedResults.ValidationProblem(errors, error.Description),
            ErrorType.NotFound => TypedResults.NotFound(),
            ErrorType.Conflict => TypedResults.Conflict(),
            _ => TypedResults.Problem(
                detail: error.Description,
                extensions: errors.ToDictionary(s => s.Key, s => (object?)s.Value)),
        };
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
        var errors = GetErrorsDictionary(error);

        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return TypedResults.Problem(
            detail: error.Description,
            statusCode: statusCode,
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
}
