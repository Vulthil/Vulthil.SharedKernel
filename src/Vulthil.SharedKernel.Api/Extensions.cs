using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vulthil.Framework.Results;
using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Api;

public static class Extensions
{
    public static ActionResult ToValidationProblemDetailsActionResult(this Result result, ControllerBase controller, int? statusCode = null)
    {
        return controller.ValidationProblem(result, statusCode);
    }

    public static ActionResult ValidationProblem(this ControllerBase controller, Result result, int? statusCode = null)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException();
        }

        if (result.Error is ValidationError validationError)
        {
            foreach (var error in validationError.Errors)
            {
                controller.ModelState.AddModelError(error.Code, error.Description);
            }
        }
        else
        {
            controller.ModelState.AddModelError(result.Error.Code, result.Error.Description);
        }

        statusCode ??= GetStatusCode(result.Error.Type);

        return controller.ValidationProblem(statusCode: statusCode);
    }

    private static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Problem => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
}
