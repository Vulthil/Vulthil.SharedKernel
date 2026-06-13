using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.TestHarness;

/// <summary>
/// Open-generic builders for in-memory dispatch handlers. The methods are the single source of truth for the
/// dispatch closure shape; <see cref="InMemoryHandlerFactory"/> binds to them via typed delegates, so signature
/// drift fails at startup. Public on an internal class so reflection binds with <c>BindingFlags.Public</c>.
/// </summary>
internal static class InMemoryMessageHandlers
{
    /// <summary>Builds a handler for a one-way <see cref="IConsumer{TMessage}"/>.</summary>
    public static InMemoryHandler ForConsumer<TConsumer, TMessage>()
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
        => new(HandlerKind.Consumer, async (scope, message, envelope, ct) =>
        {
            var consumer = scope.GetRequiredService<TConsumer>();
            var harness = scope.GetRequiredService<TestHarness>();
            var context = InMemoryContext.Create(scope, (TMessage)message, envelope, ct);

            var pipeline = ConsumePipelineFactory.Build<TMessage>(scope, terminal: c =>
            {
                harness.RecordConsumed((TMessage)message, envelope);
                return consumer.ConsumeAsync(c, c.CancellationToken);
            });

            await pipeline(context);
            return null;
        });

    /// <summary>Builds a handler for a request/reply <see cref="IRequestConsumer{TRequest, TResponse}"/>.</summary>
    public static InMemoryHandler ForRequestConsumer<TConsumer, TRequest, TResponse>()
        where TConsumer : class, IRequestConsumer<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : notnull
        => new(HandlerKind.RequestConsumer, async (scope, message, envelope, ct) =>
        {
            var consumer = scope.GetRequiredService<TConsumer>();
            var provider = scope.GetRequiredService<IMessageConfigurationProvider>();
            var harness = scope.GetRequiredService<TestHarness>();
            var options = provider.JsonSerializerOptions;
            var context = InMemoryContext.Create(scope, (TRequest)message, envelope, ct);

            try
            {
                TResponse response = default!;
                var produced = false;

                var pipeline = ConsumePipelineFactory.Build<TRequest>(scope, terminal: async c =>
                {
                    harness.RecordConsumed((TRequest)message, envelope);
                    response = await consumer.ConsumeAsync(c, c.CancellationToken);
                    produced = true;
                });

                await pipeline(context);

                return (MessageEnvelope?)(produced
                    ? InMemoryReply.Build(provider.GetUrn(typeof(TResponse)), JsonSerializer.SerializeToElement(response, options), envelope)
                    : InMemoryReply.BuildFault(
                        "Consume pipeline did not produce a response (a filter likely short-circuited the chain).",
                        typeof(InvalidOperationException).FullName!,
                        stackTrace: null,
                        options,
                        envelope));
            }
            catch (Exception ex)
            {
                return InMemoryReply.BuildFault(ex, options, envelope);
            }
        });
}
