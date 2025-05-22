using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Vulthil.SharedKernel.Application.Behaviors;
internal sealed class RequestLoggingPipelineBehavior<TRequest>(
    ILogger<RequestLoggingPipelineBehavior<TRequest>> logger,
    ICommandHandler<TRequest> innerHandler)
    : ICommandHandler<TRequest>
    where TRequest : class, ICommand
{
    private readonly ILogger<RequestLoggingPipelineBehavior<TRequest>> _logger = logger;
    private readonly ICommandHandler<TRequest> _innerHandler = innerHandler;

    public async Task<Result> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Processing request {RequestName}", requestName);

        var result = await _innerHandler.HandleAsync(command, cancellationToken);

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

internal sealed class RequestLoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> logger,
    ICommandHandler<TRequest, TResponse> innerHandler)
    : ICommandHandler<TRequest, TResponse>
    where TRequest : class, ICommand<TResponse>
    where TResponse : class
{
    private readonly ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> _logger = logger;
    private readonly ICommandHandler<TRequest, TResponse> _innerHandler = innerHandler;

    public async Task<Result<TResponse>> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Processing request {RequestName}", requestName);

        var result = await _innerHandler.HandleAsync(command, cancellationToken);

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
