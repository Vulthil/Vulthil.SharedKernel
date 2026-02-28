using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application;

namespace WebApi.Application;
/// <summary>
/// Represents the DependencyInjection.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
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
