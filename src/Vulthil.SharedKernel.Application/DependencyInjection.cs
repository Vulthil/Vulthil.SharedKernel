using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Events;

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

        services.Scan(s => s.FromAssemblies(applicationOptions.MediatRAssemblies)
            .AddClasses(c => c.AssignableTo(typeof(IHandler<,>)), false).AsImplementedInterfaces(t => t.GetGenericTypeDefinition() == typeof(IHandler<,>)).WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IDomainEventHandler<>)), false).AsImplementedInterfaces().WithScopedLifetime());

        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddScoped<ISender, Sender>();

        foreach (var item in applicationOptions.PipelineHandlers)
        {
            services.Add(item);
        }

        return services;
    }
}
