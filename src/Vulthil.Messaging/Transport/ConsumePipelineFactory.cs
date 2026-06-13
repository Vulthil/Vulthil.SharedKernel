using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Composes the registered <see cref="IConsumeFilter{TMessage}"/> instances into a single delegate.
/// Transport authors call this in their consume path to wrap a terminal stage (the consumer invocation)
/// with the configured middleware.
/// </summary>
public static class ConsumePipelineFactory
{
    /// <summary>
    /// Composes the registered <see cref="IConsumeFilter{TMessage}"/> instances around a terminal
    /// delegate. The first filter resolved from DI becomes the outermost; the terminal delegate
    /// runs innermost.
    /// </summary>
    /// <typeparam name="TMessage">The message type whose filters are composed.</typeparam>
    /// <param name="sp">The scope's service provider used to resolve the filters.</param>
    /// <param name="terminal">The innermost delegate, typically the consumer invocation.</param>
    /// <returns>A delegate that runs all filters in order and then the terminal.</returns>
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
        for (var i = filters.Length - 1; i >= 0; i--)
        {
            var filter = filters[i];
            var next = pipeline;
            pipeline = context => filter.ConsumeAsync(context, next);
        }

        return pipeline;
    }
}
