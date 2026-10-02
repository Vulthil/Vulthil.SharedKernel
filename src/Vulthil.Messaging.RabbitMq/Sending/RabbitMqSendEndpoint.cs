using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Sending;

internal sealed class RabbitMqSendEndpoint : ISendEndpoint
{
    private readonly IInternalPublisher _publisher;
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqSendEndpoint> _logger;
    private readonly string _queueName;

    public RabbitMqSendEndpoint(
        Uri address,
        string queueName,
        IInternalPublisher publisher,
        IMessageConfigurationProvider messageConfigurationProvider,
        ILogger<RabbitMqSendEndpoint> logger)
    {
        Address = address;
        _queueName = queueName;
        _publisher = publisher;
        _messageConfigurationProvider = messageConfigurationProvider;
        _logger = logger;
    }

    public Uri Address { get; }

    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : notnull
        => SendAsync(message, null, cancellationToken);

    public async Task SendAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configureContext = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var publishContext = new PublishContext();
        configureContext ??= (_ => ValueTask.CompletedTask);
        await configureContext(publishContext).ConfigureAwait(false);

        var messageConfiguration = _messageConfigurationProvider.GetMessageConfiguration(message.GetType());
        var send = RabbitMqOutgoingMessages.Send(message, publishContext, messageConfiguration, _queueName, _messageConfigurationProvider.JsonSerializerOptions);

        using var activity = send.StartActivity();
        MessagingLog.Sending(_logger, send.Ids.UrnString, _queueName, send.Ids.MessageId, send.Ids.CorrelationId);

        try
        {
            await _publisher.InternalSendAsync(send.Message, cancellationToken).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
