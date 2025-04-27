using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Messaging.Publishers;

namespace Vulthil.SharedKernel.Messaging.RabbitMq;

public static class MessagingConfiguratorExtensions
{
    public static IMessagingConfigurator UseRabbitMq(this IMessagingConfigurator configurator, IConfiguration configuration, string connectionStringKey)
    {
        configurator.Services.AddOptions<RabbitMqOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString(connectionStringKey) ?? throw new InvalidOperationException("Connection string for RabbitMq is required"));

        configurator.Services.AddSingleton<RabbitMqConnectionFactory>();

        configurator.Services.AddHostedService<Subscriber>();

        configurator.Services.AddSingleton<IPublisher, RabbitMqPublisher>();

        return configurator;
    }

    public static IHostApplicationBuilder AddRabbitMqClient(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder.AddRabbitMQClient(connectionStringKey);

        return builder;
    }
}
