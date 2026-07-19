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
#pragma warning disable IDE0032 // Use auto property
    private ILogger? _logger;
#pragma warning restore IDE0032

    /// <summary>
    /// Gets a logger categorized for the concrete controller type, lazily resolved from the request services
    /// on first access and cached for the lifetime of this controller instance.
    /// </summary>
    protected ILogger Logger => _logger ??= HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
}
