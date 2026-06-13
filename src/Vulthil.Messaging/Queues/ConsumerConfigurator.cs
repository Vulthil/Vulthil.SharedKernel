using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

/// <summary>
/// Concrete consumer configurator. Inherits <see cref="BaseConfigurator{TConfigurator}.UseRetry"/>
/// returning <see cref="IConsumerConfigurator{TConsumer}"/> — no body needed.
/// </summary>
/// <typeparam name="TConsumer">The consumer type.</typeparam>
internal sealed class ConsumerConfigurator<TConsumer>
    : BaseConfigurator<IConsumerConfigurator<TConsumer>>, IConsumerConfigurator<TConsumer>
    where TConsumer : IConsumer;
