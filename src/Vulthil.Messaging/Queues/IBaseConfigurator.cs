namespace Vulthil.Messaging.Queues;

/// <summary>
/// Self-typed base interface for fluent consumer configurators. <typeparamref name="TConfigurator"/>
/// is the derived configurator interface (e.g. <see cref="IConsumerConfigurator{TConsumer}"/>), so
/// <see cref="UseRetry"/> returns it directly — concrete configurator classes inherit
/// <see cref="BaseConfigurator{TConfigurator}"/> without needing their own explicit interface implementations.
/// </summary>
public interface IBaseConfigurator<TConfigurator>
    where TConfigurator : IBaseConfigurator<TConfigurator>
{
    /// <summary>Configures a retry policy and returns the typed configurator for chaining.</summary>
    TConfigurator UseRetry(Action<RetryPolicyConfigurator> value);
}
