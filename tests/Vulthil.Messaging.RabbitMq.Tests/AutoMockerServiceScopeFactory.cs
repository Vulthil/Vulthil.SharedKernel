using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Adapts an <see cref="IServiceProvider"/> — typically an <c>AutoMocker</c> instance, which itself implements
/// <see cref="IServiceProvider"/> — into an <see cref="IServiceScopeFactory"/> whose scopes resolve directly
/// from it. Lets a test exercise a transport's real create-scope-per-delivery code path without standing up a
/// full DI container.
/// </summary>
internal sealed class AutoMockerServiceScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new PassthroughServiceScope(serviceProvider);

    private sealed class PassthroughServiceScope(IServiceProvider serviceProvider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public void Dispose()
        {
        }
    }
}
