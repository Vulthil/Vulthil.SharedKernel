using Catalog.Search.Contracts;
using Catalog.Search.Indexing;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging;
using Vulthil.Messaging.Inbox;
using Vulthil.SharedKernel.Api;
using Vulthil.SharedKernel.Application;

namespace Catalog.Search;

/// <summary>
/// The entry points of the search module. The host calls <c>AddApplication</c> once for its own handlers and the
/// shared pipeline behaviors; this module adds its own assembly with the per-assembly entry points, and the behaviors
/// apply to its handlers too.
/// </summary>
public static class SearchModule
{
    public const string IndexQueueName = "catalog-search-index";

    public static IServiceCollection AddCatalogSearchModule(this IServiceCollection services)
    {
        var assembly = typeof(SearchModule).Assembly;

        services.AddHandlers(handlers => handlers.RegisterHandlerAssemblies(assembly));
        services.AddFluentValidation(validation => validation.RegisterFluentValidationAssemblies(assembly));
        services.AddEndpoints(assembly);

        return services;
    }

    public static IMessagingConfigurator AddCatalogSearchMessaging(this IMessagingConfigurator messaging)
    {
        messaging.ConfigureQueue(IndexQueueName, queue =>
        {
            queue.UseRetry(retry => retry.SetIntervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
            queue.UseDeadLetterQueue();
            queue.AddConsumer<ProductPublishedConsumer>();
        });

        messaging.AddIdempotentInbox<ProductPublished>();

        return messaging;
    }
}
