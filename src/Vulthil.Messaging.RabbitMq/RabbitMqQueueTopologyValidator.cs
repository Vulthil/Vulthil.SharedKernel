using Microsoft.Extensions.Options;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// Validates the configured queue definitions against the RabbitMQ topology contract when the host starts
/// (through <c>ValidateOnStart</c> on <see cref="RabbitMqTransportOptions"/>). A queue's own exchange must be
/// <see cref="MessagingExchangeType.Fanout"/>: the queue is bound to its exchange with an empty routing key,
/// and retry re-deliveries dead-letter back into that exchange preserving the original routing key — with a
/// Direct, Topic, or Headers exchange both normal and retry deliveries become unroutable and the broker drops
/// them silently. Selective routing belongs on the message exchange:
/// <see cref="MessageConfiguration.ExchangeType"/> combined with
/// <see cref="IQueueConfigurator.Subscribe{TMessage}"/> binding patterns. A quorum queue cannot be
/// <see cref="QueueDefinition.Exclusive"/>: RabbitMQ has no exclusive quorum queues, so the transport would
/// declare the queue without the setting.
/// </summary>
internal sealed class RabbitMqQueueTopologyValidator(IMessageConfigurationProvider provider) : IValidateOptions<RabbitMqTransportOptions>
{
    public ValidateOptionsResult Validate(string? name, RabbitMqTransportOptions options)
    {
        var failures = provider.QueueDefinitions.SelectMany(Failures).ToList();

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static IEnumerable<string> Failures(QueueDefinition queue)
    {
        if (queue.ExchangeType != MessagingExchangeType.Fanout)
        {
            yield return
                $"Queue '{queue.Name}' sets ExchangeType '{queue.ExchangeType}', but a queue's own exchange must be Fanout: " +
                "the queue is bound to it with an empty routing key, and retry re-deliveries dead-letter back into it with the " +
                "original routing key, so a non-fanout exchange silently drops both normal and retry deliveries. Route " +
                "selectively via the message exchange instead (MessageConfiguration<TMessage>.ExchangeType plus " +
                "Subscribe<TMessage>(routingKey) binding patterns).";
        }

        if (queue.IsQuorum && queue.Exclusive)
        {
            yield return
                $"Queue '{queue.Name}' sets Exclusive, but it is a quorum queue (IsQuorum is true by default), and RabbitMQ " +
                "cannot declare an exclusive quorum queue, so the setting would be ignored. Set IsQuorum to false for an " +
                "exclusive classic queue, or remove Exclusive.";
        }
    }
}
