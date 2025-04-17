namespace Vulthil.SharedKernel.Messaging;

public sealed record QueueDefinition(string Name, Dictionary<Type, List<Type>> Consumers);
