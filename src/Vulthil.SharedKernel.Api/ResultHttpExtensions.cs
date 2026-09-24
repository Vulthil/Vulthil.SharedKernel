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

        var result = await resultTask.ConfigureAwait(false);
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

        var result = await resultTask.ConfigureAwait(false);
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
    /// Converts a <see cref="Result{T}"/> to a typed <see cref="IResult"/>: <c>201 Created</c> with the value and a
    /// <c>Location</c> for the named route on success, otherwise the problem response for the error (see
    /// <see cref="CustomResults.Problem"/>). The success member documents itself for OpenAPI; declare the error types the
    /// endpoint can produce with <see cref="ProducesErrorsExtensions.ProducesErrors{TBuilder}"/> or
    /// <see cref="ProducesErrorAttribute"/> so exactly those problem responses are documented.
    /// </summary>
    public static Results<CreatedAtRoute<T>, ProblemHttpResult> ToCreatedAtRouteHttpResult<T>(this Result<T> result, string? routeName = null, Func<T, object?>? routeValueFactory = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return TypedResults.CreatedAtRoute(result.Value, routeName, routeValueFactory != null ? routeValueFactory(result.Value) : null);
        }

        return CustomResults.Problem(result.Error);
    }

    /// <summary>
    /// Converts a <see cref="Result{T}"/> to a typed <see cref="IResult"/>: <c>200 OK</c> with the value on success,
    /// otherwise the problem response for the error (see <see cref="CustomResults.Problem"/>). The success member
    /// documents itself for OpenAPI; declare the error types the endpoint can produce with
    /// <see cref="ProducesErrorsExtensions.ProducesErrors{TBuilder}"/> or <see cref="ProducesErrorAttribute"/> so exactly
    /// those problem responses are documented.
    /// </summary>
    public static Results<Ok<T>, ProblemHttpResult> ToIResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return CustomResults.Problem(result.Error);
    }

    /// <summary>
    /// Converts a <see cref="Result"/> to a typed <see cref="IResult"/>: <c>204 No Content</c> on success, otherwise the
    /// problem response for the error (see <see cref="CustomResults.Problem"/>). The success member documents itself for
    /// OpenAPI; declare the error types the endpoint can produce with
    /// <see cref="ProducesErrorsExtensions.ProducesErrors{TBuilder}"/> or <see cref="ProducesErrorAttribute"/> so exactly
    /// those problem responses are documented.
    /// </summary>
    public static Results<NoContent, ProblemHttpResult> ToIResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return TypedResults.NoContent();
        }

        return CustomResults.Problem(result.Error);
    }

    /// <summary>
    /// Converts an <see cref="Error"/> to the same <see cref="ProblemHttpResult"/> problem response a failed result
    /// produces (see <see cref="CustomResults.Problem"/>).
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
}

/// <summary>
/// Provides factory methods for creating problem-detail HTTP responses from <see cref="Error"/> values.
/// </summary>
public static class CustomResults
{
    /// <summary>
    /// Creates the RFC 7807 problem response for an <see cref="Error"/>, with the status code mapped from its
    /// <see cref="ErrorType"/> and the description as <c>detail</c>. A <see cref="ErrorType.Validation"/> error produces
    /// an <see cref="HttpValidationProblemDetails"/> body whose <c>errors</c> map lists each inner error by code; every
    /// other error type produces a <see cref="ProblemDetails"/> body carrying the error code as an extension. Every
    /// failure path of the typed-result extensions goes through this method, so a failed result and a bare error yield
    /// the same response.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <returns>A <see cref="ProblemHttpResult"/> representing the problem response.</returns>
    public static ProblemHttpResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var errors = GetErrorsDictionary(error);
        var statusCode = GetStatusCode(error.Type);

        if (error.Type == ErrorType.Validation)
        {
            return TypedResults.Problem(new HttpValidationProblemDetails(errors)
            {
                Detail = error.Description,
                Status = statusCode,
            });
        }

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
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
