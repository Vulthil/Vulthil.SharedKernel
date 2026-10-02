using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Logging;
using Vulthil.Messaging.RabbitMq.Telemetry;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Publishing;

internal sealed class RabbitMqPublisher : ITransportPublisher, IInternalPublisher
{
    private readonly IMessageConfigurationProvider _messageConfigurationProvider;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly RabbitMqChannelPool _channelPool;

    private readonly ConcurrentDictionary<string, bool> _knownExchanges = new();

    public RabbitMqPublisher(
        IMessageConfigurationProvider messageConfigurationProvider,
        RabbitMqChannelPool channelPool,
        ILogger<RabbitMqPublisher> logger)
    {
        _messageConfigurationProvider = messageConfigurationProvider;
        _channelPool = channelPool;
        _logger = logger;
    }

    /// <remarks>
    /// Publishes pub/sub over the message's fanout/topic exchange. Publisher confirmations (with tracking) make the
    /// awaited <c>BasicPublishAsync</c> wait for the broker ack and throw on a nack. The publish is not mandatory
    /// because zero bound subscribers is normal for pub/sub; broker confirms still guard against broker-side loss.
    /// Channels come from a bounded pool — each leased channel is used non-concurrently and returned for reuse, or
    /// discarded if it faults.
    /// </remarks>
    public async Task InternalPublishAsync(
        RabbitMqOutgoingMessage message,
        MessageConfiguration messageConfiguration,
        CancellationToken cancellationToken)
    {
        var channel = await _channelPool.LeaseAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureExchangeTopologyAsync(channel, message.Exchange, messageConfiguration, cancellationToken).ConfigureAwait(false);

            await PublishOnAsync(channel, message, cancellationToken).ConfigureAwait(false);
            _channelPool.Return(channel);
        }
        catch
        {
            await ReturnOrDiscardAsync(channel).ConfigureAwait(false);
            throw;
        }
    }

    /// <remarks>
    /// Sends point-to-point via the broker's default exchange, which always routes by queue name; the destination
    /// queue is owned by the receiving service and is not declared here. The publish is mandatory, so a missing
    /// destination queue makes the broker return the message and the awaited confirm throw a <c>PublishReturnException</c>.
    /// </remarks>
    public async Task InternalSendAsync(
        RabbitMqOutgoingMessage message,
        CancellationToken cancellationToken)
    {
        var channel = await _channelPool.LeaseAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PublishOnAsync(channel, message, cancellationToken).ConfigureAwait(false);
            _channelPool.Return(channel);
        }
        catch
        {
            await ReturnOrDiscardAsync(channel).ConfigureAwait(false);
            throw;
        }
    }

    public async Task PublishAsync<TMessage>(
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
        var publish = RabbitMqOutgoingMessages.Publish(message, publishContext, messageConfiguration, _messageConfigurationProvider.JsonSerializerOptions);

        using var activity = publish.StartActivity();
        MessagingLog.Publishing(_logger, publish.Ids.UrnString, publish.Message.Exchange, publish.Message.RoutingKey, publish.Ids.MessageId);

        try
        {
            await InternalPublishAsync(publish.Message, messageConfiguration, cancellationToken).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private static ValueTask PublishOnAsync(IChannel channel, RabbitMqOutgoingMessage message, CancellationToken cancellationToken) =>
        channel.BasicPublishAsync(message.Exchange, message.RoutingKey, message.Mandatory, message.Properties, message.Body, cancellationToken);

    private async ValueTask EnsureExchangeTopologyAsync(IChannel channel, string exchange, MessageConfiguration messageConfiguration, CancellationToken cancellationToken)
    {
        if (_knownExchanges.ContainsKey(exchange))
        {
            return;
        }

        // ExchangeDeclare is idempotent, so a concurrent first-publish burst that declares the same exchange on
        // several pooled channels is harmless; the cache then short-circuits subsequent publishes.
        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: messageConfiguration.ExchangeType.ToRabbitExchangeType(),
            durable: messageConfiguration.Durable,
            autoDelete: messageConfiguration.AutoDelete,
            arguments: messageConfiguration.Arguments,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _knownExchanges.TryAdd(exchange, true);
        MessagingLog.ExchangeDeclared(_logger, exchange, messageConfiguration.ExchangeType);
    }

    /// <summary>
    /// Returns a channel to the pool when it is still open and discards it only when a genuine fault has closed it.
    /// A returned (unroutable mandatory send) or nacked publish surfaces as an exception but leaves the channel
    /// healthy, so discarding it would churn the pool on every unroutable message; a closed channel is faulted and
    /// must not be reused.
    /// </summary>
    private ValueTask ReturnOrDiscardAsync(IChannel channel)
    {
        if (channel.IsOpen)
        {
            _channelPool.Return(channel);
            return ValueTask.CompletedTask;
        }

        return _channelPool.DiscardAsync(channel);
    }
}
