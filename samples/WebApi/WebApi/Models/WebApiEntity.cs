using Vulthil.SharedKernel.Events;
using Vulthil.SharedKernel.Primitives;

namespace WebApi.Models;

public sealed record WebApiEntityId(Guid Value);

public class WebApiEntity : AggregateRoot<WebApiEntityId>
{
    public string Name { get; private set; }
    private WebApiEntity(string name) : base(new(Guid.CreateVersion7())) => Name = name;
    public static WebApiEntity Create(string name)
    {
        var webApiEntity = new WebApiEntity(name);
        webApiEntity.Raise(new WebApiEntityCreatedEvent(webApiEntity.Id));

        return webApiEntity;
    }
}

public sealed record WebApiEntityCreatedEvent(WebApiEntityId Id) : IDomainEvent;
