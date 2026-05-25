using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Requests;

namespace Vulthil.Messaging.RabbitMq.Sending;

internal sealed class RabbitMqSendEndpointProvider : ISendEndpointProvider
{
    private readonly IInternalPublisher _publisher;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqSendEndpoint> _endpointLogger;
    private readonly ConcurrentDictionary<Uri, ISendEndpoint> _endpoints = new();

    public RabbitMqSendEndpointProvider(
        IInternalPublisher publisher,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILoggerFactory loggerFactory)
    {
        _publisher = publisher;
        _messageConfigurationProvider = messageConfigurationProvider;
        _endpointLogger = loggerFactory.CreateLogger<RabbitMqSendEndpoint>();
    }

    public ValueTask<ISendEndpoint> GetSendEndpointAsync(Uri address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        var endpoint = _endpoints.GetOrAdd(address, CreateEndpoint);
        return ValueTask.FromResult(endpoint);
    }

    private ISendEndpoint CreateEndpoint(Uri address)
    {
        var queueName = PublishContext.ResolveRoutingKeyFromUri(address);
        if (string.IsNullOrEmpty(queueName))
        {
            throw new ArgumentException(
                $"Send endpoint address '{address}' did not resolve to a destination queue name. " +
                "Use a 'queue:<name>' URI or an absolute amqp/rabbitmq URI whose path identifies the queue.",
                nameof(address));
        }

        return new RabbitMqSendEndpoint(address, queueName, _publisher, _messageConfigurationProvider, _endpointLogger);
    }
}
