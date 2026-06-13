namespace Vulthil.Messaging.Queues;

/// <summary>
/// Base class for fluent consumer configurators. Provides the shared retry-policy storage and a single
/// <see cref="UseRetry"/> implementation that returns the derived configurator's interface type, so
/// concrete configurator classes can inherit and remain empty.
/// </summary>
/// <typeparam name="TConfigurator">The derived configurator interface (e.g. <see cref="IConsumerConfigurator{TConsumer}"/>).</typeparam>
internal abstract class BaseConfigurator<TConfigurator> : IBaseConfigurator<TConfigurator>
    where TConfigurator : class, IBaseConfigurator<TConfigurator>
{
    internal RetryPolicyDefinition? RetryPolicy { get; private set; }

    /// <inheritdoc />
    public TConfigurator UseRetry(Action<RetryPolicyConfigurator> value)
    {
        var configurator = new RetryPolicyConfigurator();
        value(configurator);
        RetryPolicy = configurator.Build();
        return Self;
    }

    /// <summary>
    /// Returns <c>this</c> typed as <typeparamref name="TConfigurator"/>. Safe by construction:
    /// every concrete subclass of <see cref="BaseConfigurator{TConfigurator}"/> implements
    /// <typeparamref name="TConfigurator"/> per the class-level constraint.
    /// </summary>
    protected TConfigurator Self => (TConfigurator)(IBaseConfigurator<TConfigurator>)this;
}
