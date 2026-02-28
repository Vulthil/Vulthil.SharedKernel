using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

/// <summary>
/// Base class for consumer configurators providing shared retry policy support.
/// </summary>
public abstract class BaseConfigurator
{
    internal Dictionary<MessageType, string> Overrides { get; } = [];

    internal RetryPolicyDefinition? RetryPolicy { get; private set; }

    /// <summary>
    /// Configures a retry policy for this consumer.
    /// </summary>
    /// <param name="value">An action to configure the retry policy.</param>
    public void UseRetry(Action<RetryPolicyConfigurator> value)
    {
        var _retryPolicyConfigurator = new RetryPolicyConfigurator();
        value(_retryPolicyConfigurator);
        RetryPolicy = _retryPolicyConfigurator.Build();
    }
}

/// <summary>
/// Provides per-consumer configuration for routing key overrides and retry policies.
/// </summary>
/// <typeparam name="TConsumer">The consumer type being configured.</typeparam>
public sealed class ConsumerConfigurator<TConsumer> : BaseConfigurator, IConsumerConfigurator<TConsumer> where TConsumer : IConsumer
{
    /// <inheritdoc />
    public IConsumerConfigurator<TConsumer> Bind<TMessage>(string routingKey)
        where TMessage : notnull
    {
        if (!typeof(TConsumer).IsAssignableTo(typeof(IConsumer<TMessage>)))
        {
            throw new ArgumentException(
                $"Registration Error: '{typeof(TConsumer).Name}' cannot bind to '{typeof(TMessage).Name}' " +
                $"because it does not implement IConsumer<{typeof(TMessage).Name}>.");
        }

        Overrides[new(typeof(TMessage))] = routingKey;
        return this;
    }

}

/// <summary>
/// Configures per-consumer routing key overrides and retry policies.
/// </summary>
/// <typeparam name="TConsumer">The consumer type.</typeparam>
public interface IConsumerConfigurator<TConsumer> where TConsumer : IConsumer
{
    /// <summary>
    /// Binds a message type to a routing key for the configured consumer.
    /// </summary>
    /// <typeparam name="TMessage">The message type to bind.</typeparam>
    /// <param name="routingKey">The routing key to use for the message type.</param>
    /// <returns>The current configurator instance.</returns>
    IConsumerConfigurator<TConsumer> Bind<TMessage>(string routingKey)
        where TMessage : notnull;
    /// <summary>
    /// Configures a retry policy for this consumer.
    /// </summary>
    /// <param name="value">An action to configure the retry policy.</param>
    void UseRetry(Action<RetryPolicyConfigurator> value);
}
/// <summary>
/// Configures per-consumer routing key overrides for request/reply consumers.
/// </summary>
/// <typeparam name="TConsumer">The request consumer type.</typeparam>
public interface IRequestConfigurator<TConsumer> where TConsumer : IRequestConsumer
{
    /// <summary>
    /// Binds a request/response message pair to a routing key for the configured request consumer.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <param name="routingKey">The routing key to use for the request binding.</param>
    /// <returns>The current request configurator instance.</returns>
    IRequestConfigurator<TConsumer> Bind<TRequest, TResponse>(string routingKey)
        where TRequest : notnull
        where TResponse : notnull;
    /// <summary>
    /// Configures a retry policy for this request consumer.
    /// </summary>
    /// <param name="value">An action to configure the retry policy.</param>
    void UseRetry(Action<RetryPolicyConfigurator> value);
}
