using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application;

namespace Orders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        services.AddApplication(appOptions =>
        {
            appOptions.AddRequestLoggingBehavior()
                .AddDomainEventLoggingBehavior()
                .AddValidationPipelineBehavior()
                .AddTransactionalPipelineBehavior()
                .RegisterHandlerAssemblies(typeof(DependencyInjection).Assembly)
                .RegisterFluentValidationAssemblies(typeof(DependencyInjection).Assembly);
        });

        return services;
    }
}
