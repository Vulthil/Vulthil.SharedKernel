using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Configurator extensions that opt a message type into idempotent (inbox) processing.
/// </summary>
/// <remarks>
/// Opt-in is per message type: the idempotency guard is applied to every consumer of <c>TMessage</c>. A
/// registered <see cref="IIdempotencyStore"/> is required at consume time — reference
/// <c>Vulthil.Messaging.Inbox.Relational</c> (or supply your own store) to provide one.
/// </remarks>
public static class InboxConfiguratorExtensions
{
    /// <summary>
    /// Enables idempotent processing for deliveries of <typeparamref name="TMessage"/>. The guard skips messages
    /// whose idempotency key has already been processed and otherwise runs the consumer inside an
    /// <see cref="IIdempotencyStore"/> transaction so the marker and the consumer's writes commit atomically.
    /// </summary>
    /// <typeparam name="TMessage">The message type to guard.</typeparam>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="keySelector">
    /// An optional selector for the idempotency key. When omitted (or when it returns <see langword="null"/>), the
    /// delivery's <see cref="IMessageContext.MessageId"/> is used.
    /// </param>
    /// <returns>The same configurator, for chaining.</returns>
    public static IMessagingConfigurator AddIdempotentInbox<TMessage>(
        this IMessagingConfigurator configurator,
        Func<IMessageContext<TMessage>, string?>? keySelector = null)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(configurator);

        var services = configurator.HostApplicationBuilder.Services;
        services.AddOptions<InboxOptions>();
        services.TryAddSingleton<IInboxKeySelector<TMessage>>(new DelegateInboxKeySelector<TMessage>(keySelector));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IConsumeFilter<TMessage>, IdempotentConsumeFilter<TMessage>>());

        return configurator;
    }

    /// <summary>
    /// Configures global <see cref="InboxOptions"/> shared by all idempotency-guarded message types.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <param name="configure">An action that configures the options.</param>
    /// <returns>The same configurator, for chaining.</returns>
    public static IMessagingConfigurator ConfigureInbox(
        this IMessagingConfigurator configurator,
        Action<InboxOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(configure);

        var services = configurator.HostApplicationBuilder.Services;
        services.AddOptions<InboxOptions>();
        services.Configure(configure);

        return configurator;
    }
}
