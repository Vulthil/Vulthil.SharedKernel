using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application.Behaviors;
internal sealed class RequestLoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineHandler<TRequest, TResponse>
    where TRequest : IHaveResponse<TResponse>
    where TResponse : Result
{
    private readonly ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> _logger = logger;

    public async Task<Result<TResponse>> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Processing request {RequestName}", requestName);

        var result = await next(cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Completed request {RequestName}", requestName);
        }
        else
        {
            using (_logger.BeginScope(new Dictionary<string, string>
            {
                ["Error"] = JsonSerializer.Serialize(result.Error)
            }))
            {
                _logger.LogError("Completed request {RequestName} with error", requestName);
            }
        }

        return result;
    }
}
