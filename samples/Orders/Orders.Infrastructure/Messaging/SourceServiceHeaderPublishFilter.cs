using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Transport;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// A publish filter: it runs for every publish and send of this service and stamps the <see cref="HeaderName"/>
/// header with the application name, unless the caller set it already. The Shipments sample logs it when it handles
/// a command.
/// </summary>
public sealed class SourceServiceHeaderPublishFilter(IHostEnvironment environment) : IPublishFilter
{
    public const string HeaderName = "source-service";

    public Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next)
    {
        if (!context.Context.Headers.ContainsKey(HeaderName))
        {
            context.Context.AddHeader(HeaderName, environment.ApplicationName);
        }

        return next(context);
    }
}
