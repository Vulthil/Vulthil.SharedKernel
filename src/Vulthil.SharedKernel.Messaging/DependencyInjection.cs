using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration, Action<IMessagingConfigurator> messagingConfiguratorAction)
    {
        var messagingConfigurator = new MessagingConfigurator(services, configuration);
        messagingConfiguratorAction(messagingConfigurator);

        messagingConfigurator.RegisterTypes();

        return services;
    }
}
