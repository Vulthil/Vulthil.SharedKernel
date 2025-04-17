using System.Diagnostics.CodeAnalysis;

namespace Vulthil.SharedKernel.Messaging;

public sealed class TypeCache
{
    private readonly Dictionary<string, Type> _typeMap = [];


    public bool TryGetFromString(string typeString, [NotNullWhen(true)] out Type? type) => _typeMap.TryGetValue(typeString, out type);

    internal void AddTypeMap(Type type) => _typeMap[type.FullName!] = type;

}
