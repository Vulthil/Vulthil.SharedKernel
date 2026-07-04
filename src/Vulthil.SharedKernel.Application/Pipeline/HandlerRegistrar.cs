using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Pipeline;

/// <summary>
/// Discovers handler implementations in the supplied assemblies and wires them so that
/// every supported handler interface resolves through the pipeline decorator.
/// </summary>
/// <remarks>
/// The concrete handler type is never registered as a public DI service. The decorator
/// resolves the concrete handler via <see cref="ActivatorUtilities"/>, which means consumers
/// can only reach the handler through the decorated <see cref="IHandler{TRequest, TResponse}"/>,
/// <see cref="ICommandHandler{TCommand, TResponse}"/>, <see cref="ICommandHandler{TCommand}"/>
/// or <see cref="IQueryHandler{TQuery, TResponse}"/> registrations. Pipeline composition
/// happens at resolve time, so behaviors added after handler registration still apply
/// uniformly to every handler that is resolved later.
/// </remarks>
internal static class HandlerRegistrar
{
    // Reflection over our own private generic helpers — the only way to open the constrained
    // generic methods at runtime once the (TRequest, TResponse) types are known. The bypass is
    // confined to this file.
#pragma warning disable S3011
    private static readonly MethodInfo RegisterInnerAndDecoratorMethod =
        typeof(HandlerRegistrar)
            .GetMethod(nameof(RegisterInnerAndDecoratorTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo RegisterCommandAdapterMethod =
        typeof(HandlerRegistrar)
            .GetMethod(nameof(RegisterCommandAdapterTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo RegisterCommandUnitAdapterMethod =
        typeof(HandlerRegistrar)
            .GetMethod(nameof(RegisterCommandUnitAdapterTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo RegisterQueryAdapterMethod =
        typeof(HandlerRegistrar)
            .GetMethod(nameof(RegisterQueryAdapterTyped), BindingFlags.NonPublic | BindingFlags.Static)!;
#pragma warning restore S3011

    public static void RegisterHandlersFromAssemblies(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var implType in GetCandidateTypes(assembly))
            {
                RegisterRequestHandlerInterfaces(services, implType);
                RegisterDomainEventHandlerInterfaces(services, implType);
            }
        }
    }

    private static Type[] GetCandidateTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = Array.FindAll(ex.Types, t => t is not null)!;
        }

        return Array.FindAll(types, IsConcreteCandidate);
    }

    private static bool IsConcreteCandidate(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false };

    private static void RegisterRequestHandlerInterfaces(IServiceCollection services, Type implType)
    {
        foreach (var iface in implType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IHandler<,>))
            {
                RegisterRequestHandlerInterface(services, implType, iface);
            }
        }
    }

    private static void RegisterRequestHandlerInterface(IServiceCollection services, Type implType, Type handlerInterface)
    {
        var requestType = handlerInterface.GenericTypeArguments[0];
        var responseType = handlerInterface.GenericTypeArguments[1];

        RegisterInnerAndDecoratorMethod
            .MakeGenericMethod(requestType, responseType)
            .Invoke(null, [services, implType]);

        if (typeof(ICommand<>).MakeGenericType(responseType).IsAssignableFrom(requestType))
        {
            RegisterCommandAdapterMethod
                .MakeGenericMethod(requestType, responseType)
                .Invoke(null, [services]);
        }

        if (typeof(IQuery<>).MakeGenericType(responseType).IsAssignableFrom(requestType))
        {
            RegisterQueryAdapterMethod
                .MakeGenericMethod(requestType, responseType)
                .Invoke(null, [services]);
        }

        if (responseType == typeof(Result) && typeof(ICommand).IsAssignableFrom(requestType))
        {
            RegisterCommandUnitAdapterMethod
                .MakeGenericMethod(requestType)
                .Invoke(null, [services]);
        }
    }

    private static void RegisterInnerAndDecoratorTyped<TRequest, TResponse>(IServiceCollection services, Type implType)
        where TRequest : IRequest<TResponse>
    {
        services.TryAdd(ServiceDescriptor.Scoped<IInnerHandler<TRequest, TResponse>>(sp =>
        {
            var inner = (IHandler<TRequest, TResponse>)ActivatorUtilities.CreateInstance(sp, implType);
            return new InnerHandlerAdapter<TRequest, TResponse>(inner);
        }));

        services.TryAdd(ServiceDescriptor.Scoped<IHandler<TRequest, TResponse>, PipelineHandlerDecorator<TRequest, TResponse>>());
    }

    private static void RegisterCommandAdapterTyped<TCommand, TResponse>(IServiceCollection services)
        where TCommand : ICommand<TResponse> => services.TryAdd(ServiceDescriptor.Scoped<ICommandHandler<TCommand, TResponse>>(sp =>
                                                         new CommandHandlerAdapter<TCommand, TResponse>(sp.GetRequiredService<IHandler<TCommand, TResponse>>())));

    private static void RegisterCommandUnitAdapterTyped<TCommand>(IServiceCollection services)
        where TCommand : ICommand => services.TryAdd(ServiceDescriptor.Scoped<ICommandHandler<TCommand>>(sp =>
                                              new CommandHandlerUnitAdapter<TCommand>(sp.GetRequiredService<IHandler<TCommand, Result>>())));

    private static void RegisterQueryAdapterTyped<TQuery, TResponse>(IServiceCollection services)
        where TQuery : IQuery<TResponse> => services.TryAdd(ServiceDescriptor.Scoped<IQueryHandler<TQuery, TResponse>>(sp =>
                                                     new QueryHandlerAdapter<TQuery, TResponse>(sp.GetRequiredService<IHandler<TQuery, TResponse>>())));

    private static void RegisterDomainEventHandlerInterfaces(IServiceCollection services, Type implType)
    {
        foreach (var iface in implType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            {
                services.TryAddEnumerable(new ServiceDescriptor(iface, implType, ServiceLifetime.Scoped));
            }
        }
    }
}
