using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;

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
            return HttpResults.Ok(result.Value);
        }

        return ((Result)result).ToIResult();
    }

    public static IResult ToIResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return HttpResults.NoContent();
        }
        return result.Error.ToIResult();
    }
    public static IResult ToIResult(this Error error) => CustomResults.Problem(error);

}

public static class CustomResults
{
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
