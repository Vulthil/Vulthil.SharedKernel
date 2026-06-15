using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vulthil.SharedKernel.Api;

/// <summary>
/// Abstract base API controller with preconfigured route conventions and a lazily-resolved logger.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// Gets a logger categorized for the concrete controller type, resolved from the request services.
    /// </summary>
    protected ILogger Logger => HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
}
