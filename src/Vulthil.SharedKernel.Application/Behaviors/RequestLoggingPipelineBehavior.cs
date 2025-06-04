using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Behaviors;

internal static class LoggingBehaviors
{
    internal sealed class RequestLoggingPipelineBehavior<TRequest, TResponse>(
        ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> logger)
        : IPipelineHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : Result
    {
        private readonly ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> _logger = logger;

        public async Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
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

    internal sealed class DomainEventLoggingPipelineBehavior<TDomainEvent>(
        ILogger<DomainEventLoggingPipelineBehavior<TDomainEvent>> logger)
        : IDomainEventPipelineHandler<TDomainEvent>
        where TDomainEvent : IDomainEvent
    {
        private readonly ILogger<DomainEventLoggingPipelineBehavior<TDomainEvent>> _logger = logger;

        public async Task HandleAsync(TDomainEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default)
        {
            var domainEventName = typeof(TDomainEvent).Name;

            _logger.LogInformation("Processing event {DomainEventName}", domainEventName);

            try
            {
                await next(cancellationToken);
                _logger.LogInformation("Completed processing event {DomainEventName}", domainEventName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {DomainEventName}", domainEventName);
                throw;
            }
        }
    }
}
