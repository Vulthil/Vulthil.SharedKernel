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
    /// Converts an <see cref="Error"/> to an <see cref="IActionResult"/> that writes the same RFC 7807 problem response as
    /// the minimal-API path (see <see cref="CustomResults.Problem"/>), so a controller and a minimal API endpoint answer
    /// the same error with the same status and body.
    /// </summary>
    public static IActionResult ToActionResult(this Error error, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(controller);

        return new ProblemHttpActionResult(CustomResults.Problem(error));
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
    /// failure path of the typed-result and action-result extensions goes through this method, so a failed result and a
    /// bare error yield the same response on the minimal-API and the MVC path.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <returns>A <see cref="ProblemHttpResult"/> representing the problem response.</returns>
    public static ProblemHttpResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return TypedResults.Problem(CreateProblemDetails(error));
    }

    /// <summary>
    /// Builds the problem body for an <see cref="Error"/> (see <see cref="Problem"/>). Every problem response for an
    /// error starts from this body — a failed result, a bare error, and an exception that carries an error — and the
    /// problem-details service completes it with the type, title, instance and trace identifiers.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <returns>The problem body, with its <see cref="ProblemDetails.Status"/> set.</returns>
    internal static ProblemDetails CreateProblemDetails(Error error)
    {
        var errors = GetErrorsDictionary(error);
        var statusCode = GetStatusCode(error.Type);

        if (error.Type == ErrorType.Validation)
        {
            return new HttpValidationProblemDetails(errors)
            {
                Detail = error.Description,
                Status = statusCode,
            };
        }

        var problemDetails = new ProblemDetails
        {
            Detail = error.Description,
            Status = statusCode,
        };

        foreach (var (code, descriptions) in errors)
        {
            problemDetails.Extensions[code] = descriptions;
        }

        return problemDetails;
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
