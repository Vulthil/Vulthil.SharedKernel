using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.Messaging.Outbox;

/// <summary>
/// Messaging-configurator extensions that enable the transactional bus-publish outbox.
/// </summary>
public static class TransactionalOutboxExtensions
{
    /// <summary>
    /// Captures publishes and sends issued while a database transaction is active into the shared outbox table and
    /// relays them to the broker after the transaction commits. Requires the shared outbox engine to be enabled on
    /// the application's <c>DbContext</c> (<c>EnableOutboxProcessing</c>), whose context must implement
    /// <see cref="ISaveOutboxMessages"/>.
    /// </summary>
    /// <param name="configurator">The messaging configurator.</param>
    /// <returns>The same configurator, for chaining.</returns>
    public static IMessagingConfigurator AddTransactionalOutbox(this IMessagingConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.AddPublishFilter<TransactionalPublishFilter>();

        var services = configurator.HostApplicationBuilder.Services;
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOutboxDispatcher, BrokerOutboxDispatcher>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOutboxRelayGate, TransportReadinessOutboxGate>());

        return configurator;
    }

    /// <summary>
    /// Runs the consumer of <typeparamref name="TMessage"/> inside a database transaction, so its database writes and
    /// any messages it publishes commit atomically and are captured by the transactional outbox — forcing the
    /// transaction around a consumer that does not otherwise open one (e.g. one without the inbox). If the inbox is
    /// also enabled for the type it opens the transaction first and this joins it. Requires a relational provider.
    /// </summary>
    /// <typeparam name="TMessage">The consumed message type.</typeparam>
    /// <param name="configurator">The messaging configurator.</param>
    /// <returns>The same configurator, for chaining.</returns>
    public static IMessagingConfigurator AddTransactionalConsumer<TMessage>(this IMessagingConfigurator configurator)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.HostApplicationBuilder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IConsumeFilter<TMessage>, TransactionalConsumeFilter<TMessage>>());

        return configurator;
    }
}
