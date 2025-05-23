using System.Diagnostics.CodeAnalysis;

namespace Vulthil.Messaging;

public sealed class TypeCache
{
    private readonly Dictionary<string, MessageType> _typeMap = [];
    private readonly Dictionary<Type, RequestOption> _requestOptions = [];
    private readonly Dictionary<Type, EventOption> _eventOptions = [];

    public bool RequestsConfigured => _requestOptions.Count != 0;

    public bool TryGetRequest<TRequestType>([NotNullWhen(true)] out RequestOption? requestOption) where TRequestType : notnull =>
        _requestOptions.TryGetValue(typeof(TRequestType), out requestOption);
    public bool TryGetEvent<TMessage>([NotNullWhen(true)] out EventOption? eventOption) =>
        _eventOptions.TryGetValue(typeof(TMessage), out eventOption);
    public bool TryGetFromString(string typeString, [NotNullWhen(true)] out MessageType? type) =>
        _typeMap.TryGetValue(typeString, out type);
    internal void AddTypeMap(MessageType type) => _typeMap[type.Name] = type;
    internal void AddRequestOption<TRequestType>(RequestOption requestOption) where TRequestType : notnull =>
        _requestOptions[typeof(TRequestType)] = requestOption;
    internal void AddEventOption<TEvent>(EventOption eventOption) where TEvent : notnull =>
        _eventOptions[typeof(TEvent)] = eventOption;
}
