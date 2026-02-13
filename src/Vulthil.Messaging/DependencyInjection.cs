using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Vulthil.Messaging;

public static class DependencyInjection
{
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
