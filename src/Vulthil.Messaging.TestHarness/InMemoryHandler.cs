using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// In-memory dispatch handler stored in a <see cref="MessageExecutionPlan{THandler}"/>. <see cref="Dispatch"/>
/// runs one registered consumer for a delivered message; a one-way consumer returns <see langword="null"/>,
/// a request consumer returns the reply <see cref="MessageEnvelope"/>.
/// </summary>
/// <param name="Kind">The consumer contract this handler implements.</param>
/// <param name="Dispatch">Resolves the consumer from the scope, builds the context, runs the consume pipeline, and (for requests) returns the reply.</param>
internal sealed record InMemoryHandler(
    HandlerKind Kind,
    Func<IServiceProvider, object, MessageEnvelope, CancellationToken, Task<MessageEnvelope?>> Dispatch);
