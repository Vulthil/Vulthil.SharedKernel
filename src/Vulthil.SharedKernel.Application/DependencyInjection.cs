using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, Action<ApplicationOptions>? applicationOptionsAction = null)
    {
        var applicationOptions = new ApplicationOptions();
        applicationOptionsAction?.Invoke(applicationOptions);

        return services.AddApplication(applicationOptions);
    }

    public static IServiceCollection AddApplication(this IServiceCollection services, ApplicationOptions applicationOptions)
    {
        services.AddFluentValidation(applicationOptions.FluentValidationOptions);
        services.AddHandlers(applicationOptions.HandlerOptions);
        return services;
    }

    public static IServiceCollection AddFluentValidation(this IServiceCollection services, Action<FluentValidationOptions>? fluentValidationOptionsAction = null)
    {
        var fluentValidationOptions = new FluentValidationOptions();
        fluentValidationOptionsAction?.Invoke(fluentValidationOptions);

        return services.AddFluentValidation(fluentValidationOptions);
    }
    public static IServiceCollection AddFluentValidation(this IServiceCollection services, FluentValidationOptions fluentValidationOptions)
    {
        if (fluentValidationOptions.FluentValidationAssemblies.Count != 0)
        {
            services.AddValidatorsFromAssemblies(fluentValidationOptions.FluentValidationAssemblies,
            includeInternalTypes: true);
        }
        return services;
    }

    public static IServiceCollection AddHandlers(this IServiceCollection services, Action<HandlerOptions>? handlerOptionsAction = null)
    {
        var handlerOptions = new HandlerOptions();
        handlerOptionsAction?.Invoke(handlerOptions);

        return services.AddHandlers(handlerOptions);
    }

    public static IServiceCollection AddHandlers(this IServiceCollection services, HandlerOptions handlerOptions)
    {
        if (handlerOptions.HandlerAssemblies.Count == 0)
        {
            throw new InvalidOperationException($"Must add atleast one assembly, by using the {nameof(HandlerOptions.RegisterHandlerAssemblies)} method.");
        }

        services.TryAddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.TryAddScoped<ISender, Sender>();

        services.Scan(s => s.FromAssemblies(handlerOptions.HandlerAssemblies)
            .AddClasses(c => c.AssignableTo(typeof(IHandler<,>)), false).AsImplementedInterfaces(t => t.GetGenericTypeDefinition() == typeof(IHandler<,>)).WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IDomainEventHandler<>)), false).AsImplementedInterfaces().WithScopedLifetime());

        foreach (var item in handlerOptions.PipelineHandlers)
        {
            services.TryAddEnumerable(item);
        }

        return services;
    }
}
