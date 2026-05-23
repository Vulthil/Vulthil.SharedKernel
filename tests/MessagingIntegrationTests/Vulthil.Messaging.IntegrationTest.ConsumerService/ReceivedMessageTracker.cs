using System.Collections.Concurrent;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService;

public sealed class ReceivedMessageTracker
{
    private readonly ConcurrentQueue<object> _events = new();
    private readonly ConcurrentQueue<object> _commands = new();
    private readonly ConcurrentQueue<object> _requests = new();

    public void RecordEvent(object message) => _events.Enqueue(message);
    public void RecordCommand(object message) => _commands.Enqueue(message);
    public void RecordRequest(object message) => _requests.Enqueue(message);

    public IReadOnlyCollection<object> Events => _events.ToArray();
    public IReadOnlyCollection<object> Commands => _commands.ToArray();
    public IReadOnlyCollection<object> Requests => _requests.ToArray();
}
