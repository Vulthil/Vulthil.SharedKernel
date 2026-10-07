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
    /// An optional selector for the idempotency key. When omitted, an <see cref="IInboxKeySelector{TMessage}"/> the
    /// application registered itself supplies the key, if there is one. Without one, or when the key resolves to
    /// <see langword="null"/> or an empty string, the delivery's <see cref="IMessageContext.MessageId"/> is used.
    /// </param>
    /// <returns>The same configurator, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// An earlier call for <typeparamref name="TMessage"/> passed a different key selector, or passed none where this
    /// call passes one (or the reverse); or <paramref name="keySelector"/> is passed while the application has
    /// registered its own <see cref="IInboxKeySelector{TMessage}"/>.
    /// </exception>
    /// <remarks>
    /// A message type is deduplicated on one key, so every call for the same <typeparamref name="TMessage"/>, from any
    /// module, must agree on it: all pass no selector, or all pass the same one (the same method on the same target,
    /// such as one static lambda or method group). Any other combination throws instead of silently keeping one key,
    /// which could skip distinct messages as duplicates or process a duplicate twice. To compute the key with services
    /// from dependency injection, register your own <see cref="IInboxKeySelector{TMessage}"/> and call this method
    /// without a selector; an <see cref="IInboxKeySelector{TMessage}"/> registered after this call replaces the
    /// selector it registered, because dependency injection resolves the last registration.
    /// </remarks>
    public static IMessagingConfigurator AddIdempotentInbox<TMessage>(
        this IMessagingConfigurator configurator,
        Func<IMessageContext<TMessage>, string?>? keySelector = null)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(configurator);

        var services = configurator.HostApplicationBuilder.Services;
        RegisterKeySelector(services, keySelector);
        services.AddOptions<InboxOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IConsumeFilter<TMessage>, IdempotentConsumeFilter<TMessage>>());

        return configurator;
    }

    private static void RegisterKeySelector<TMessage>(IServiceCollection services, Func<IMessageContext<TMessage>, string?>? keySelector)
        where TMessage : notnull
    {
        var registered = services.LastOrDefault(static descriptor =>
            descriptor.ServiceType == typeof(IInboxKeySelector<TMessage>) && !descriptor.IsKeyedService);

        if (registered is null)
        {
            services.AddSingleton<IInboxKeySelector<TMessage>>(new DelegateInboxKeySelector<TMessage>(keySelector));
            return;
        }

        if (registered.ImplementationInstance is DelegateInboxKeySelector<TMessage> delegateSelector)
        {
            if (!Equals(delegateSelector.KeySelector, keySelector))
            {
                throw new InvalidOperationException(
                    $"AddIdempotentInbox was already called for '{typeof(TMessage).FullName}' with a different key selector. " +
                    "A message type is deduplicated on one key, so keeping one selector and dropping the other could skip " +
                    "distinct messages as duplicates or process a duplicate twice. Pass the same key selector (the same method " +
                    "on the same target), or none, in every AddIdempotentInbox call for this message type.");
            }

            return;
        }

        if (keySelector is not null)
        {
            throw new InvalidOperationException(
                $"An IInboxKeySelector is already registered for '{typeof(TMessage).FullName}', so the key selector passed to " +
                "AddIdempotentInbox would be dropped. Call AddIdempotentInbox without a key selector to use the registered " +
                "IInboxKeySelector, or remove that registration to use the passed key selector.");
        }
    }
}
