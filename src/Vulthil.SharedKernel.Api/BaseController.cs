using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Abstract base API controller with preconfigured route conventions and logging.
/// </summary>
/// <param name="logger">The logger instance for the controller.</param>
[ApiController]
[Route("api/[controller]")]
public abstract class BaseController(ILogger logger) : ControllerBase
{
    /// <summary>
    /// Gets the logger instance scoped to the derived controller type.
    /// </summary>
    protected ILogger Logger { get; } = logger;
}
