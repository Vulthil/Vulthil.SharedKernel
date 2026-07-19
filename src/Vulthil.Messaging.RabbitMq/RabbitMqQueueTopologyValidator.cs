using Microsoft.Extensions.Options;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// Validates the configured queue definitions against the RabbitMQ topology contract when the host starts
/// (through <c>ValidateOnStart</c> on <see cref="RabbitMqTransportOptions"/>): a queue's own exchange must be
/// <see cref="MessagingExchangeType.Fanout"/>. The queue is bound to its exchange with an empty routing key,
/// and retry re-deliveries dead-letter back into that exchange preserving the original routing key — with a
/// Direct, Topic, or Headers exchange both normal and retry deliveries become unroutable and the broker drops
/// them silently. Selective routing belongs on the message exchange:
/// <see cref="MessageConfiguration.ExchangeType"/> combined with
/// <see cref="Queues.IQueueConfigurator.Subscribe{TMessage}"/> binding patterns.
/// </summary>
internal sealed class RabbitMqQueueTopologyValidator(IMessageConfigurationProvider provider) : IValidateOptions<RabbitMqTransportOptions>
{
    public ValidateOptionsResult Validate(string? name, RabbitMqTransportOptions options)
    {
        var failures = provider.QueueDefinitions
            .Where(static queue => queue.ExchangeType != MessagingExchangeType.Fanout)
            .Select(static queue =>
                $"Queue '{queue.Name}' sets ExchangeType '{queue.ExchangeType}', but a queue's own exchange must be Fanout: " +
                "the queue is bound to it with an empty routing key, and retry re-deliveries dead-letter back into it with the " +
                "original routing key, so a non-fanout exchange silently drops both normal and retry deliveries. Route " +
                "selectively via the message exchange instead (MessageConfiguration<TMessage>.ExchangeType plus " +
                "Subscribe<TMessage>(routingKey) binding patterns).")
            .ToList();

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
