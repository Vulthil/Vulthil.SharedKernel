using System.Collections.Concurrent;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// The type part of the outbox row format: the name a row stores for its message type, and the lookup that finds the
/// type again when the relay reads the row. Every capture and every dispatcher uses it, so they agree on the format.
/// </summary>
/// <remarks>
/// A row stores the type's <see cref="Type.FullName"/>, without the assembly, so a row stays readable when the
/// assembly version changes. The lookup searches every assembly loaded in the process and caches the result per name.
/// </remarks>
public static class OutboxMessageTypes
{
    private static readonly ConcurrentDictionary<string, Type> ResolvedTypes = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the name an outbox row stores for <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The message type.</param>
    /// <returns>The type's full name.</returns>
    /// <exception cref="ArgumentException"><paramref name="type"/> has no full name, for example a generic type parameter.</exception>
    public static string NameOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.FullName
            ?? throw new ArgumentException($"The type '{type}' has no full name, so an outbox row cannot store it.", nameof(type));
    }

    /// <summary>
    /// Finds the type that an outbox row's type name refers to, among the assemblies loaded in the process. A type that
    /// several assemblies expose through a type forward counts once. An assembly-qualified name is resolved through the
    /// assembly it names.
    /// </summary>
    /// <param name="typeName">The type name the row stores.</param>
    /// <returns>The type.</returns>
    /// <exception cref="InvalidOperationException">
    /// No loaded assembly defines the type, or two loaded assemblies define different types with this name.
    /// </exception>
    public static Type Resolve(string typeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);

        return ResolvedTypes.GetOrAdd(typeName, Find);
    }

    private static Type Find(string typeName)
    {
        // Type.GetType resolves an assembly-qualified name, which names its assembly and so cannot be ambiguous, and a
        // type of the core library or of this assembly; every other name needs the scan below.
        if (Type.GetType(typeName) is { } directType)
        {
            return directType;
        }

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .OfType<Type>()
            .Distinct()
            .ToList();

        return types switch
        {
            [var type] => type,
            [] => throw new InvalidOperationException(
                $"Unable to resolve the outbox message type '{typeName}': no loaded assembly defines it. " +
                "Ensure the assembly that defines the type is loaded in the relay process."),
            _ => throw new InvalidOperationException(
                $"The outbox message type '{typeName}' is ambiguous: the assemblies " +
                string.Join(", ", types.Select(static type => $"'{type.Assembly.FullName}'")) +
                " each define a different type with this name. Load only one of them in the relay process."),
        };
    }
}
