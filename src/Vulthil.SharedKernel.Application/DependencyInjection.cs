using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Behaviours;

namespace Vulthil.SharedKernel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, Action<ApplicationOptions>? applicationOptionsAction = null)
    {
        var applicationOptions = new ApplicationOptions();
        applicationOptionsAction?.Invoke(applicationOptions);

        if (applicationOptions.FluentValidationAssemblies.Count != 0)
        {
            services.AddValidatorsFromAssemblies(applicationOptions.FluentValidationAssemblies,
                includeInternalTypes: true);
        }

        if (applicationOptions.MediatRAssemblies.Count == 0)
        {
            throw new InvalidOperationException($"Must add atleast one assembly, by using the {nameof(ApplicationOptions.RegisterMediatRAssemblies)} method.");
        }

        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblies([.. applicationOptions.MediatRAssemblies]);

            if (applicationOptions.AddRequestLoggingBehaviour)
            {
                options.AddOpenBehavior(typeof(RequestLoggingPipelineBehavior<,>));
            }

            if (applicationOptions.AddValidationPipelineBehaviour)
            {
                options.AddOpenBehavior(typeof(ValidationPipelineBehaviour<,>));
            }

            if (applicationOptions.AddTransactionalPipelineBehaviour)
            {
                options.AddOpenBehavior(typeof(TransactionalPipelineBehaviour<,>));
            }
        });

        return services;
    }
}
