using System.Collections.Concurrent;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;

public sealed class ReceivedMessageTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<object>> _messages = new();
    private readonly ConcurrentDictionary<Guid, int> _attempts = new();

    public void Record(string key, object message)
        => _messages.GetOrAdd(key, _ => new ConcurrentQueue<object>()).Enqueue(message);

    public IReadOnlyCollection<object> Get(string key)
        => _messages.TryGetValue(key, out var queue) ? queue.ToArray() : [];

    public int RecordAttempt(Guid id) => _attempts.AddOrUpdate(id, 1, (_, count) => count + 1);

    public int GetAttempts(Guid id) => _attempts.GetValueOrDefault(id);
}
