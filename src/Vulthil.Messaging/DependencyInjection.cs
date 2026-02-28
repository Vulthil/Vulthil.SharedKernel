using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Vulthil.Messaging;

/// <summary>
/// Provides extension methods for registering messaging services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers messaging services, queues, and consumers with the host application builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="messagingConfiguratorAction">An action to configure messaging through <see cref="IMessagingConfigurator"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessaging(this IHostApplicationBuilder builder, Action<IMessagingConfigurator> messagingConfiguratorAction)
    {
        var messagingOptions = new MessagingOptions();
        builder.Configuration.GetSection(MessagingOptions.SectionName).Bind(messagingOptions);

        builder.Services.AddHostedService<ConsumerHostedService>();

        var messagingConfigurator = new MessagingConfigurator(builder, messagingOptions);
        messagingConfiguratorAction(messagingConfigurator);

        builder.Services.AddSingleton(Options.Create(messagingOptions));

        return builder.Services;
    }

}
