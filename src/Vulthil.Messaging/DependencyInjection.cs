using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Filters;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

/// <summary>
/// Provides extension methods for registering messaging services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers messaging services, queues, and consumers with the host application builder.
    /// </summary>
    /// <remarks>
    /// Queue definitions and message configurations under <c>Messaging:Queues:*</c> and <c>Messaging:Messages:*</c>
    /// are loaded from <see cref="IConfiguration"/> before <paramref name="messagingConfiguratorAction"/> runs.
    /// Code registrations performed inside the configurator action merge onto the loaded values and take precedence.
    /// Built-in consume filters (see <see cref="ConsumeFilterOptions"/>) are registered before the configurator
    /// action runs, so user-registered filters compose INSIDE the defaults.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="messagingConfiguratorAction">An action to configure messaging through <see cref="IMessagingConfigurator"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessaging(this IHostApplicationBuilder builder, Action<IMessagingConfigurator> messagingConfiguratorAction)
    {
        var messagingOptions = new MessagingOptions();
        builder.Configuration.GetSection(MessagingOptions.SectionName).Bind(messagingOptions);

        LoadQueueDefinitionsFromConfiguration(builder.Configuration, messagingOptions);
        LoadMessageConfigurationsFromConfiguration(builder.Configuration, messagingOptions);

        builder.Services.AddHostedService<ConsumerHostedService>();

        var messagingConfigurator = new MessagingConfigurator(builder, messagingOptions);
        messagingConfiguratorAction(messagingConfigurator);

        RegisterDefaultConsumeFilters(builder.Services, messagingOptions);

        return builder.Services;
    }

    /// <remarks>
    /// Runs after the configurator action so a default filter can be toggled via <c>ConfigureMessagingOptions</c>; a
    /// disabled filter is never added to DI (there is no runtime <c>IsEnabled</c> check). Each default is inserted at
    /// index 0 so its open-generic descriptor resolves first in <c>IEnumerable&lt;IConsumeFilter&lt;T&gt;&gt;</c>,
    /// keeping it outermost in the composed pipeline even though it registers last.
    /// </remarks>
    private static void RegisterDefaultConsumeFilters(IServiceCollection services, MessagingOptions options)
    {
        if (options.ConsumeFilters.EnableLogging)
        {
            services.Insert(0, new ServiceDescriptor(
                typeof(IConsumeFilter<>),
                typeof(LoggingConsumeFilter<>),
                ServiceLifetime.Scoped));
        }
    }

    private static void LoadQueueDefinitionsFromConfiguration(IConfiguration configuration, MessagingOptions options)
    {
        var queuesSection = configuration.GetSection($"{MessagingConfigurator.DefaultSectionName}:Queues");
        foreach (var queueSection in queuesSection.GetChildren())
        {
            var name = queueSection.Key;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var queueDefinition = new QueueDefinition(name);
            queueSection.Bind(queueDefinition);
            options.QueueDefinitions[name] = queueDefinition;
        }
    }

    private static void LoadMessageConfigurationsFromConfiguration(IConfiguration configuration, MessagingOptions options)
    {
        var messagesSection = configuration.GetSection($"{MessagingConfigurator.DefaultSectionName}:Messages");
        foreach (var messageSection in messagesSection.GetChildren())
        {
            var fullName = messageSection.Key;
            if (string.IsNullOrWhiteSpace(fullName))
            {
                continue;
            }

            var messageConfiguration = new MessageConfiguration(fullName);
            messageSection.Bind(messageConfiguration);
            options.MessageConfigurations[fullName] = messageConfiguration;
        }
    }
}
