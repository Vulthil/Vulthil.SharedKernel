using System.Collections.Concurrent;
using Shipments.Application.Notifications;
using Vulthil.Extensions.Testing;

namespace Shipments.Tests.Fixtures;

/// <summary>
/// Records the notifications instead of sending them. As an <see cref="IResettableTestState"/> it is cleared after
/// every test, so a test only sees the notifications of its own shipments.
/// </summary>
public sealed class InMemoryCustomerNotifier : ICustomerNotifier, IResettableTestState
{
    private readonly ConcurrentQueue<DeliveryNotification> _sent = new();

    public IReadOnlyCollection<DeliveryNotification> Sent => _sent;

    public Task NotifyDeliveredAsync(DeliveryNotification notification, CancellationToken cancellationToken)
    {
        _sent.Enqueue(notification);
        return Task.CompletedTask;
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        _sent.Clear();
        return ValueTask.CompletedTask;
    }
}
