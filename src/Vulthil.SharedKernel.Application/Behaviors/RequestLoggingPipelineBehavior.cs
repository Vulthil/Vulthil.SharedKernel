using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Behaviors;

internal static partial class LoggingBehaviors
{
    internal sealed partial class RequestLoggingPipelineBehavior<TRequest, TResponse>(
        ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> logger)
        : IPipelineHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : Result
    {
        private readonly ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> _logger = logger;

        public async Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
        {
            var requestName = typeof(TRequest).Name;

            LogProcessingRequest(_logger, requestName);

            var result = await next(cancellationToken);

            if (result.IsSuccess)
            {
                LogCompletedRequest(_logger, requestName);
            }
            else
            {
                using (_logger.BeginScope(new Dictionary<string, string>
                {
                    ["Error"] = JsonSerializer.Serialize(result.Error)
                }))
                {
                    LogCompletedRequestWithError(_logger, requestName);
                }
            }

            return result;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing request {RequestName}")]
        private static partial void LogProcessingRequest(ILogger logger, string requestName);
        [LoggerMessage(Level = LogLevel.Information, Message = "Completed request {RequestName}")]
        private static partial void LogCompletedRequest(ILogger logger, string requestName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Completed request {RequestName} with error")]
        private static partial void LogCompletedRequestWithError(ILogger logger, string requestName);
    }

    internal sealed partial class DomainEventLoggingPipelineBehavior<TDomainEvent>(
        ILogger<DomainEventLoggingPipelineBehavior<TDomainEvent>> logger)
        : IDomainEventPipelineHandler<TDomainEvent>
        where TDomainEvent : IDomainEvent
    {
        private readonly ILogger<DomainEventLoggingPipelineBehavior<TDomainEvent>> _logger = logger;

        public async Task HandleAsync(TDomainEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default)
        {
            var domainEventName = typeof(TDomainEvent).Name;

            LogProcessingEvent(_logger, domainEventName);

            await next(cancellationToken);

            LogCompletedEvent(_logger, domainEventName);
        }


        [LoggerMessage(Level = LogLevel.Information, Message = "Processing event {DomainEventName}")]
        private static partial void LogProcessingEvent(ILogger logger, string domainEventName);
        [LoggerMessage(Level = LogLevel.Information, Message = "Completed processing event {DomainEventName}")]
        private static partial void LogCompletedEvent(ILogger logger, string domainEventName);
    }
}
