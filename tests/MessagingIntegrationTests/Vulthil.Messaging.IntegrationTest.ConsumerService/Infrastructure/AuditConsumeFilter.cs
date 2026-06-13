using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;

public sealed class AuditConsumeFilter<TMessage>(ReceivedMessageTracker tracker) : IConsumeFilter<TMessage>
    where TMessage : notnull
{
    public async Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next)
    {
        tracker.Record("filter", typeof(TMessage).Name);
        await next(context);
    }
}
