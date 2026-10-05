# Vulthil.Messaging

Use `Vulthil.Messaging` to configure messaging pipelines and hosted consumers.

## When to use

- Service registration for consumers and queues
- Runtime message handling orchestration

## Pattern

- Keep wiring in composition root
- Separate message contracts from processing logic
- Centralize retry/error strategy decisions

## Usage

### Registering queues and consumers

```csharp
builder.AddMessaging(messaging =>
{
    messaging.ConfigureQueue("order-events", queue =>
    {
        queue.AddConsumer<OrderCreatedConsumer>();
        queue.UseRetry(retry =>
        {
            retry.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
            retry.UseJitter(0.2);
        });
        queue.UseDeadLetterQueue();
    });

    messaging.ConfigureQueue("order-requests", queue =>
    {
        queue.AddRequestConsumer<GetOrderConsumer>();
    });
});
```

### Routing key configuration

```csharp
messaging.ConfigureMessage<OrderCreatedEvent>(message =>
{
    message.UseRoutingKey(e => $"order.{e.Region}");
    message.UseCorrelationId(e => e.OrderId.ToString());
});
```

### Per-consumer retry override

`AddConsumer` accepts a configurator; its knob is the consumer's retry policy, which overrides the queue-level
default for that consumer alone:

```csharp
queue.AddConsumer<OrderCreatedConsumer>(c =>
{
    c.UseRetry(r => r.Immediate(5));
});
```

Binding patterns are configured per queue, not per consumer — use
`queue.Subscribe<OrderCreatedEvent>("order.eu")` (see [Messaging — Routing Keys](../messaging.md#routing-keys)).

### Configuration-driven setup

Queue and message settings under `Messaging:Queues:*` and `Messaging:Messages:*`
are loaded from `IConfiguration` before the configurator action runs, so a service
can be configured entirely via `appsettings.json`. Code calls merge on top of the
loaded values, with code winning on conflict. See
[Messaging — Configuration-driven Setup](../messaging.md#configuration-driven-setup).

### Addresses and reserved headers

Addresses (`SendAsync` destinations, response and fault addresses) are URIs; a bare name denotes a queue. The rules
live in `Vulthil.Messaging.Transport.MessageAddress`, which every producer and consumer path shares:
`MessageAddress.Queue("order-commands")` builds a `queue:` address, `MessageAddress.Parse` turns a stored or wire value
back into one (a bare name becomes `queue:<name>`), and `MessageAddress.QueueName` reads the name out of a `queue:`
address. Transport authors use the same three calls, so a reply-to queue name, a header value and a `queue:` URI always
denote the same destination.

The metadata a publish context carries (`ConversationId`, `InitiatorId`, `SourceAddress`, `DestinationAddress`,
`ResponseAddress`, `FaultAddress`) travels under reserved header keys that the envelope promotes to typed fields. The
names are the constants on `Vulthil.Messaging.Transport.MessageHeaders`, and `MessageHeaders.IsReserved(key)` tells
whether a key is one of them — a custom header must not reuse these keys.

### Publish and send terminals

A transport implements two raw terminals: `ITransportPublisher`, and `ITransportSendEndpointProvider`, which hands out
one `ITransportSendEndpoint` per address. It registers them and calls `AddPublishFiltering`, which puts the public
`IPublisher` and `ISendEndpointProvider` in front of them. The public facade runs the caller's `configure` callback and
the publish filters on one `PublishContext`, then hands the terminal the message and that resolved context. The outbox
relay calls the same terminals with the context it stored. `MessageEnvelopeFactory.Create` serializes the payload as
the message's runtime type, so a message held as a base type or an interface keeps every member of its concrete type.

### Delivery dispatch

`Vulthil.Messaging.Transport.DeliveryDispatcher` owns the delivery rules a transport needs: consumers run in rounds that
re-run only the consumers that failed, every attempt runs in its own DI scope, each consumer retries under its own
policy and publishes its `Fault<T>` once it has failed for good, and a request consumer runs once and replies. A
transport builds its plans with `MessageExecutionRegistry<DeliveryHandler>` and `DeliveryHandlerFactory`, implements
`IDeliveryPort` for each delivery (the receive context, the retry wait, and the fault and reply routes), and applies the
`DeliverySettlement` the dispatcher returns. `AddMessaging` registers the dispatcher. The RabbitMQ transport and the
test harness both run on it.

A transport that needs its own handler type can instead derive from `MessageHandlerFactory<THandler>` and override its
two generic methods (`CreateConsumerHandler<TConsumer, TMessage>` and
`CreateRequestConsumerHandler<TConsumer, TRequest, TResponse>`): the base class binds every registration's CLR types to
those overrides and caches the binding per consumer/message shape. See
[Messaging — Writing a Custom Transport](../messaging.md#writing-a-custom-transport).

### Request/reply codec

`Vulthil.Messaging.Transport.RpcReply` owns the reply wire contract. A transport builds a reply with
`RpcReply.Success`, `RpcReply.Fault` (a consumer exception) or `RpcReply.ShortCircuited` (a filter ended the pipeline),
and reads one back with `RpcReply.ToResult<TResponse>`, which never throws: an unreadable reply becomes a
`Messaging.Request.Deserialize` failure. The RabbitMQ transport and the in-memory test harness both use it, so a reply
means the same on each.
