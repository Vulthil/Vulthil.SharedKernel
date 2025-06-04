using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application;

namespace WebApi.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddApplication(appOptions =>
        {
            appOptions.AddRequestLoggingBehavior()
                .AddDomainEventLoggingBehavior()
                .AddValidationPipelineBehavior()
                .AddTransactionalPipelineBehavior()
                .RegisterMediatRAssemblies(typeof(DependencyInjection).Assembly)
                .RegisterFluentValidationAssemblies(typeof(DependencyInjection).Assembly);
        });

        return services;
    }
}
