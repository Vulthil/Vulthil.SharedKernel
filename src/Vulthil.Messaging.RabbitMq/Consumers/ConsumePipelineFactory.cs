using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal static class ConsumePipelineFactory
{
    /// <summary>
    /// Composes the registered <see cref="IConsumeFilter{TMessage}"/> instances around a terminal
    /// delegate. The first filter resolved from DI becomes the outermost; the terminal delegate
    /// runs innermost.
    /// </summary>
    public static ConsumeDelegate<TMessage> Build<TMessage>(
        IServiceProvider sp,
        ConsumeDelegate<TMessage> terminal)
        where TMessage : notnull
    {
        var filters = sp.GetServices<IConsumeFilter<TMessage>>().ToArray();
        if (filters.Length == 0)
        {
            return terminal;
        }

        var pipeline = terminal;
        // Iterate in reverse so the first-registered filter ends up outermost.
        for (var i = filters.Length - 1; i >= 0; i--)
        {
            var filter = filters[i];
            var next = pipeline;
            pipeline = context => filter.ConsumeAsync(context, next);
        }

        return pipeline;
    }
}
