using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Framework.Results;
using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Api;

public static class Extensions
{
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return result.Error.ToActionResult(controller);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return result.Error.ToActionResult(controller);
    }

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

    public static IResult ToIResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ((Result)result).ToIResult();
    }

    public static IResult ToIResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }
        return result.Error.ToIResult();
    }
    public static IResult ToIResult(this Error error)
    {
        Dictionary<string, string[]> errors;

        if (error is ValidationError validationError)
        {
            errors = validationError.Errors
                .GroupBy(e => e.Code, s => s.Description)
                .ToDictionary(e => e.Key, errors => errors.ToArray());
        }
        else
        {
            errors = new Dictionary<string, string[]>
            {
                [error.Code] = [error.Description]
            };
        }

        return error.Type switch
        {
            ErrorType.Validation => Results.ValidationProblem(errors, error.Description),
            ErrorType.NotFound => Results.NotFound(),

            _ => Results.Problem(extensions: errors.ToDictionary(s => s.Key, s => (object?)s.Value)),
        };
    }
}
