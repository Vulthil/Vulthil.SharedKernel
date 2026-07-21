using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application;

/// <summary>
/// Provides extension methods for registering application-layer services.
/// </summary>
public static class DependencyInjection
{

    /// <summary>
    /// Registers application-layer services with no handler assemblies configured.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This overload always throws <see cref="InvalidOperationException"/> at runtime because it registers zero
    /// handler assemblies. Use <see cref="AddApplication(IServiceCollection, Action{ApplicationOptions})"/> and call
    /// <see cref="ApplicationOptions.RegisterHandlerAssemblies"/> inside the configuration action instead.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Always thrown because no handler assemblies are registered.</exception>
    // S1133 flags every [Obsolete] member as a reminder to delete it; removal is tracked separately as a
    // breaking change for the next major version, so the reminder is redundant for these trap overloads.
#pragma warning disable S1133
    [Obsolete("This overload always throws InvalidOperationException because it registers zero handler assemblies. Call AddApplication(Action<ApplicationOptions>) and use options.RegisterHandlerAssemblies(...) instead.")]
    public static IServiceCollection AddApplication(this IServiceCollection services) => services.AddApplication(new ApplicationOptions());
#pragma warning restore S1133

    /// <summary>
    /// Registers application-layer services including handlers and FluentValidation validators.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="applicationOptionsAction">An action to configure application options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This is the single intended entry point for registering the application layer. Configure handler
    /// assemblies, validator assemblies, and pipeline behaviors on the supplied <see cref="ApplicationOptions"/>
    /// inside <paramref name="applicationOptionsAction"/> rather than calling <see cref="AddHandlers(IServiceCollection)"/>
    /// or <see cref="AddFluentValidation(IServiceCollection)"/> directly.
    /// </remarks>
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
    /// Registers FluentValidation validators with no assemblies configured.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This overload silently registers no validators because it scans zero assemblies — it does not throw, so
    /// the gap is easy to miss. Use <see cref="AddApplication(IServiceCollection, Action{ApplicationOptions})"/>
    /// and call <see cref="ApplicationOptions.RegisterFluentValidationAssemblies"/> inside the configuration
    /// action instead.
    /// </remarks>
    // S1133: see the rationale on the AddApplication() trap overload above — removal is tracked separately.
#pragma warning disable S1133
    [Obsolete("This overload silently registers no validators because it scans zero assemblies. Call AddApplication(Action<ApplicationOptions>) and use options.RegisterFluentValidationAssemblies(...) instead.")]
    public static IServiceCollection AddFluentValidation(this IServiceCollection services) => services.AddFluentValidation(new FluentValidationOptions());
#pragma warning restore S1133


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
    /// Registers request handlers, domain event handlers, and pipeline handlers with no handler assemblies configured.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This overload always throws <see cref="InvalidOperationException"/> at runtime because it registers zero
    /// handler assemblies. Use <see cref="AddApplication(IServiceCollection, Action{ApplicationOptions})"/> and call
    /// <see cref="ApplicationOptions.RegisterHandlerAssemblies"/> inside the configuration action instead.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Always thrown because no handler assemblies are registered.</exception>
    // S1133: see the rationale on the AddApplication() trap overload above — removal is tracked separately.
#pragma warning disable S1133
    [Obsolete("This overload always throws InvalidOperationException because it registers zero handler assemblies. Call AddApplication(Action<ApplicationOptions>) and use options.RegisterHandlerAssemblies(...) instead.")]
    public static IServiceCollection AddHandlers(this IServiceCollection services) => services.AddHandlers(new HandlerOptions());
#pragma warning restore S1133

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
            throw new InvalidOperationException($"Must add at least one assembly, by using the {nameof(HandlerOptions.RegisterHandlerAssemblies)} method.");
        }

        services.TryAddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.TryAddScoped<ISender, Sender>();

        HandlerRegistrar.RegisterHandlersFromAssemblies(services, handlerOptions.HandlerAssemblies);

        foreach (var item in handlerOptions.PipelineHandlers)
        {
            services.TryAddEnumerable(item);
        }

        return services;
    }

    /// <summary>
    /// Registers an open-generic request pipeline behavior. Behaviors registered through this method
    /// apply to every handler resolved after <see cref="IServiceProvider"/> construction — order of
    /// registration relative to <see cref="AddApplication(IServiceCollection, Action{ApplicationOptions})"/>
    /// is irrelevant because behaviors are composed lazily at handler-resolution time.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pipelineHandler">The open-generic type implementing <see cref="IPipelineHandler{TRequest, TResponse}"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is not a valid open-generic <see cref="IPipelineHandler{TRequest, TResponse}"/>.</exception>
    public static IServiceCollection AddOpenPipelineHandler(this IServiceCollection services, Type pipelineHandler)
    {
        ArgumentNullException.ThrowIfNull(services);

        OpenGenericPipelineHandler.EnsureValid(pipelineHandler, typeof(IPipelineHandler<,>));

        services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineHandler<,>), pipelineHandler, ServiceLifetime.Scoped));
        return services;
    }

    /// <summary>
    /// Registers an open-generic domain event pipeline behavior. Behaviors registered through this
    /// method apply lazily and are independent of when handlers were registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pipelineHandler">The open-generic type implementing <see cref="IDomainEventPipelineHandler{TDomainEvent}"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is not a valid open-generic <see cref="IDomainEventPipelineHandler{TDomainEvent}"/>.</exception>
    public static IServiceCollection AddOpenDomainEventPipelineHandler(this IServiceCollection services, Type pipelineHandler)
    {
        ArgumentNullException.ThrowIfNull(services);

        OpenGenericPipelineHandler.EnsureValid(pipelineHandler, typeof(IDomainEventPipelineHandler<>));

        services.TryAddEnumerable(new ServiceDescriptor(typeof(IDomainEventPipelineHandler<>), pipelineHandler, ServiceLifetime.Scoped));
        return services;
    }
}
