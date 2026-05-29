using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

/// <summary>
/// Concrete request-consumer configurator. Inherits <see cref="BaseConfigurator{TConfigurator}.UseRetry"/>
/// returning <see cref="IRequestConfigurator{TConsumer}"/> — no body needed.
/// </summary>
public sealed class RequestConsumerConfigurator<TConsumer>
    : BaseConfigurator<IRequestConfigurator<TConsumer>>, IRequestConfigurator<TConsumer>
    where TConsumer : IRequestConsumer;
