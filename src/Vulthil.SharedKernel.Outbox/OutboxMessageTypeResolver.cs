using System.Collections.Concurrent;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Resolves the CLR <see cref="Type"/> for an outbox row's stored type name, caching successful resolutions.
/// Shared by <see cref="DomainEventOutboxDispatcher"/> and <c>Vulthil.Messaging.Outbox.BrokerOutboxDispatcher</c>,
/// which dispatch different <see cref="OutboxDestination"/> rows but resolve their stored type name the same way.
/// </summary>
internal static class OutboxMessageTypeResolver
{
    private static readonly ConcurrentDictionary<string, Type> Cache = [];

    /// <summary>
    /// Resolves <paramref name="typeName"/> via <see cref="Type.GetType(string)"/>, falling back to a scan of every
    /// loaded assembly. A failed resolution is never cached, so a type that loads later (or a persistently
    /// unresolvable one) is retried on every call.
    /// </summary>
    /// <param name="typeName">The stored, assembly-unqualified or assembly-qualified type name.</param>
    /// <param name="typeLabel">Describes the kind of row being resolved (e.g. "domain-event", "message"), interpolated into the failure message.</param>
    /// <returns>The resolved type.</returns>
    /// <exception cref="InvalidOperationException">No loaded assembly defines a type named <paramref name="typeName"/>.</exception>
    public static Type Resolve(string typeName, string typeLabel) => Cache.GetOrAdd(typeName, name =>
    {
        var type = Type.GetType(name)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name))
                .FirstOrDefault(found => found is not null);

        return type ?? throw new InvalidOperationException(
            $"Unable to resolve the {typeLabel} type '{name}' for an outbox relay. " +
            "Ensure the assembly that defines the type is loaded in the relay process.");
    });
}
