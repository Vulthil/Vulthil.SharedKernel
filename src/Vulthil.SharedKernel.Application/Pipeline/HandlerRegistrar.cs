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
/// <para>
/// A request and response pair has exactly one handler: registering the same handler type again
/// changes nothing, and a second, different handler type throws instead of silently losing one of
/// them. Domain event handlers fan out, so every handler of an event is kept.
/// </para>
/// </remarks>
internal static class HandlerRegistrar
{
    public static void RegisterHandlersFromAssemblies(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var implType in GetCandidateTypes(assembly))
            {
                RegisterHandlerInterfaces(services, implType);
            }
        }
    }

    private static Type[] GetCandidateTypes(Assembly assembly)
    {
        try
        {
            return Array.FindAll(assembly.GetTypes(), IsConcreteCandidate);
        }
        catch (ReflectionTypeLoadException ex)
        {
            var loaderErrors = ex.LoaderExceptions.OfType<Exception>().Select(static error => error.Message).Distinct();
            throw new InvalidOperationException(
                $"Could not load every type in handler assembly '{assembly.FullName}', so a handler among the types " +
                "that failed to load would be missing without notice until a request for it fails. Fix the load " +
                "errors, which usually point to a missing or mismatched dependency assembly:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, loaderErrors)}",
                ex);
        }
    }

    private static bool IsConcreteCandidate(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false };

    private static void RegisterHandlerInterfaces(IServiceCollection services, Type implType)
    {
        foreach (var iface in implType.GetInterfaces())
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            var definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IHandler<,>))
            {
                RegisterRequestHandler(services, implType, iface.GenericTypeArguments[0], iface.GenericTypeArguments[1]);
            }
            else if (definition == typeof(IDomainEventHandler<>))
            {
                services.TryAddEnumerable(new ServiceDescriptor(iface, implType, ServiceLifetime.Scoped));
            }
        }
    }

    private static void RegisterRequestHandler(IServiceCollection services, Type handlerType, Type requestType, Type responseType)
    {
        var innerHandlerService = typeof(IInnerHandler<,>).MakeGenericType(requestType, responseType);
        var innerHandler = typeof(InnerHandlerAdapter<,,>).MakeGenericType(requestType, responseType, handlerType);
        ThrowIfAnotherHandlerIsRegistered(services, innerHandlerService, innerHandler);

        services.TryAdd(ServiceDescriptor.Scoped(innerHandlerService, innerHandler));
        TryAddScoped(services, typeof(IHandler<,>), typeof(PipelineHandlerDecorator<,>), requestType, responseType);

        if (typeof(ICommand<>).MakeGenericType(responseType).IsAssignableFrom(requestType))
        {
            TryAddScoped(services, typeof(ICommandHandler<,>), typeof(CommandHandlerAdapter<,>), requestType, responseType);
        }

        if (typeof(IQuery<>).MakeGenericType(responseType).IsAssignableFrom(requestType))
        {
            TryAddScoped(services, typeof(IQueryHandler<,>), typeof(QueryHandlerAdapter<,>), requestType, responseType);
        }

        if (responseType == typeof(Result) && typeof(ICommand).IsAssignableFrom(requestType))
        {
            TryAddScoped(services, typeof(ICommandHandler<>), typeof(CommandHandlerUnitAdapter<>), requestType);
        }
    }

    private static void TryAddScoped(IServiceCollection services, Type openService, Type openImplementation, params Type[] typeArguments) =>
        services.TryAdd(ServiceDescriptor.Scoped(openService.MakeGenericType(typeArguments), openImplementation.MakeGenericType(typeArguments)));

    private static void ThrowIfAnotherHandlerIsRegistered(IServiceCollection services, Type innerHandlerService, Type innerHandler)
    {
        var registered = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == innerHandlerService && !descriptor.IsKeyedService);

        if (registered is null || registered.ImplementationType == innerHandler)
        {
            return;
        }

        var registeredHandler = registered.ImplementationType!.GenericTypeArguments[2];
        var (request, response, handler) = (innerHandler.GenericTypeArguments[0], innerHandler.GenericTypeArguments[1], innerHandler.GenericTypeArguments[2]);
        throw new InvalidOperationException(
            $"Both '{registeredHandler}' and '{handler}' handle request '{request}' with response " +
            $"'{response}'. A request has exactly one handler, so keeping one and dropping the other would " +
            "decide without notice, by scan order, which code runs. Keep one handler for this request and response; " +
            "when one handler derives from the other, make the base class abstract.");
    }
}
