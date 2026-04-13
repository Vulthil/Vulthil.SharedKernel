using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application;

/// <summary>
/// Provides extension methods for registering application-layer services.
/// </summary>
public static class DependencyInjection
{

    /// <summary>
    /// Registers application-layer services with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services.AddApplication(new ApplicationOptions());
    }

    /// <summary>
    /// Registers application-layer services including handlers and FluentValidation validators.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="applicationOptionsAction">An action to configure application options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services, Action<ApplicationOptions> applicationOptionsAction)
    {
        var applicationOptions = new ApplicationOptions();
        applicationOptionsAction.Invoke(applicationOptions);

        return services.AddApplication(applicationOptions);
    }

    /// <summary>
    /// Registers application-layer services using a pre-configured <see cref="ApplicationOptions"/> instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="applicationOptions">The pre-configured application options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services, ApplicationOptions applicationOptions)
    {
        services.AddFluentValidation(applicationOptions.FluentValidationOptions);
        services.AddHandlers(applicationOptions.HandlerOptions);
        return services;
    }

    /// <summary>
    /// Registers FluentValidation validators with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFluentValidation(this IServiceCollection services)
    {
        return services.AddFluentValidation(new FluentValidationOptions());
    }


    /// <summary>
    /// Registers FluentValidation validators from the configured assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="fluentValidationOptionsAction">An optional action to configure validation options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFluentValidation(this IServiceCollection services, Action<FluentValidationOptions> fluentValidationOptionsAction)
    {
        var fluentValidationOptions = new FluentValidationOptions();
        fluentValidationOptionsAction.Invoke(fluentValidationOptions);

        return services.AddFluentValidation(fluentValidationOptions);
    }
    /// <summary>
    /// Registers FluentValidation validators using a pre-configured <see cref="FluentValidationOptions"/> instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="fluentValidationOptions">The pre-configured validation options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFluentValidation(this IServiceCollection services, FluentValidationOptions fluentValidationOptions)
    {
        if (fluentValidationOptions.FluentValidationAssemblies.Count != 0)
        {
            services.AddValidatorsFromAssemblies(fluentValidationOptions.FluentValidationAssemblies,
            includeInternalTypes: true);
        }
        return services;
    }

    /// <summary>
    /// Registers request handlers, domain event handlers, and pipeline handlers with default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHandlers(this IServiceCollection services)
    {
        return services.AddHandlers(new HandlerOptions());
    }

    /// <summary>
    /// Registers request handlers, domain event handlers, and pipeline handlers from the configured assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="handlerOptionsAction">An optional action to configure handler options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHandlers(this IServiceCollection services, Action<HandlerOptions> handlerOptionsAction)
    {
        var handlerOptions = new HandlerOptions();
        handlerOptionsAction.Invoke(handlerOptions);

        return services.AddHandlers(handlerOptions);
    }

    /// <summary>
    /// Registers request handlers, domain event handlers, and pipeline handlers using a pre-configured <see cref="HandlerOptions"/> instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="handlerOptions">The pre-configured handler options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no handler assemblies have been registered.</exception>
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
