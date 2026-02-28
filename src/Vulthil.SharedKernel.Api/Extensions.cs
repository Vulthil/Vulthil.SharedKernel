using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

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
    /// Converts a <see cref="Result{T}"/> to an <see cref="IResult"/> for minimal API endpoints.
    /// </summary>
    public static IResult ToIResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return HttpResults.Ok(result.Value);
        }

        return ((Result)result).ToIResult();
    }

    /// <summary>
    /// Converts a <see cref="Result"/> to an <see cref="IResult"/> for minimal API endpoints.
    /// </summary>
    public static IResult ToIResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return HttpResults.NoContent();
        }
        return result.Error.ToIResult();
    }
    /// <summary>
    /// Converts an <see cref="Error"/> to an <see cref="IResult"/> problem response for minimal API endpoints.
    /// </summary>
    public static IResult ToIResult(this Error error) => CustomResults.Problem(error);

}

/// <summary>
/// Provides factory methods for creating problem-detail HTTP responses from <see cref="Error"/> values.
/// </summary>
public static class CustomResults
{
    /// <summary>
    /// Creates a problem-detail <see cref="IResult"/> from an <see cref="Error"/>, mapping the error type to the appropriate HTTP status code.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    /// <returns>An <see cref="IResult"/> representing the problem response.</returns>
    public static IResult Problem(Error error)
    {
        Dictionary<string, string[]> errors = error is ValidationError validationError
           ? validationError.Errors
               .GroupBy(e => e.Code, s => s.Description)
               .ToDictionary(e => e.Key, errors => errors.ToArray())
           : new Dictionary<string, string[]>
           {
               [error.Code] = [error.Description]
           };

        return error.Type switch
        {
            ErrorType.Validation => HttpResults.ValidationProblem(errors, error.Description),
            ErrorType.NotFound => HttpResults.NotFound(),
            ErrorType.Conflict => HttpResults.Conflict(),

            _ => HttpResults.Problem(extensions: errors.ToDictionary(s => s.Key, s => (object?)s.Value)),
        };
    }
}
