using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.RabbitMq.Requests;

internal sealed class RequestContext : PublishContext, IRequestContext
{
    internal TimeSpan? Timeout { get; private set; }

    public IRequestContext SetTimeout(TimeSpan timeout)
    {
        Timeout = timeout;
        return this;
    }
}
