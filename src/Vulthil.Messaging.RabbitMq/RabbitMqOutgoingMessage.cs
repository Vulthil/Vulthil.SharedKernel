using System.Diagnostics;
using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.Telemetry;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// A message the RabbitMQ transport writes to the broker, complete: the exchange and routing key it is published to,
/// whether the broker must route it to a queue, and its properties and body. Built by
/// <see cref="RabbitMqOutgoingMessages"/>.
/// </summary>
/// <param name="Exchange">The exchange the message is published to; empty for the broker's default exchange.</param>
/// <param name="RoutingKey">The routing key; the destination queue's name on the default exchange.</param>
/// <param name="Mandatory">Whether the broker returns the message when no queue receives it.</param>
/// <param name="Properties">The AMQP properties.</param>
/// <param name="Body">The message body.</param>
internal sealed record RabbitMqOutgoingMessage(
    string Exchange,
    string RoutingKey,
    bool Mandatory,
    BasicProperties Properties,
    ReadOnlyMemory<byte> Body);

/// <summary>
/// A message a producer creates — a publish, a send or a request — with the identifiers resolved for it and the
/// producer activity it records. Messages derived from a delivery (a retry, a fault, a reply) record no producer
/// activity, so they are plain <see cref="RabbitMqOutgoingMessage"/>s.
/// </summary>
/// <param name="Message">The message to publish.</param>
/// <param name="Ids">The identifiers resolved for the message.</param>
/// <param name="Operation">The messaging operation: <c>publish</c>, <c>send</c> or <c>request</c>.</param>
/// <param name="Destination">The exchange, or the queue for a send, the activity names as the destination.</param>
internal sealed record RabbitMqProducedMessage(
    RabbitMqOutgoingMessage Message,
    RabbitMqMessageIds Ids,
    string Operation,
    string Destination)
{
    /// <summary>
    /// Starts the producer activity of the message with the standard Vulthil messaging tags. Returns
    /// <see langword="null"/> when no listener is recording the transport's activity source.
    /// </summary>
    /// <returns>The started activity, or <see langword="null"/>.</returns>
    public Activity? StartActivity()
    {
        var activity = MessagingInstrumentation.ActivitySource.StartActivity($"{Destination} {Operation}", ActivityKind.Producer);
        if (activity is not null)
        {
            activity.SetTag(MessagingInstrumentation.Tags.MessagingSystem, MessagingInstrumentation.SystemValue);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingOperation, Operation);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingDestination, Destination);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingRoutingKey, Message.RoutingKey);
            activity.SetTag(MessagingInstrumentation.Tags.MessageType, Ids.UrnString);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingMessageId, Ids.MessageId);
            activity.SetTag(MessagingInstrumentation.Tags.MessagingCorrelationId, Ids.CorrelationId);
        }

        return activity;
    }
}

/// <summary>The identifiers resolved for one message a producer creates.</summary>
/// <param name="CorrelationId">The business correlation identifier.</param>
/// <param name="MessageId">The message identifier.</param>
/// <param name="Urn">The stable wire URN of the message type.</param>
/// <param name="UrnString">The URN's absolute URI string, used as the AMQP <c>Type</c> and the activity's message type.</param>
internal readonly record struct RabbitMqMessageIds(string CorrelationId, string MessageId, Uri Urn, string UrnString);
