namespace Vulthil.Messaging.IntegrationTest.Contracts;

public sealed record OrderedEvent(string Key, int Sequence);
