using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        return builder.Services;
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
