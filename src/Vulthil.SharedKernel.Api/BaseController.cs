using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Api;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController(ILogger logger) : ControllerBase
{
    protected ILogger Logger { get; } = logger;

    [NonAction]
    public ActionResult ValidationProblem(Result result, int? statusCode = null)
    {
        return ((ControllerBase)this).ValidationProblem(result, statusCode);
    }
}
