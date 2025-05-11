using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Vulthil.SharedKernel.Api;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController(ILogger logger) : ControllerBase
{
    protected ILogger Logger { get; } = logger;
}
