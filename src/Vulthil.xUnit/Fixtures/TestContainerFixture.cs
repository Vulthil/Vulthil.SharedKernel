
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Base fixture that wraps a Testcontainers container as an <see cref="ITestContainer"/> for use in <see cref="BaseWebApplicationFactory{TEntryPoint}"/>.
/// </summary>
public abstract class TestContainerFixture<TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : ContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), ITestContainer, ITestContainerScopeProvider
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer
{
    /// <inheritdoc />
    public virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    /// <inheritdoc />
    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Creates a pass-through scope view over this container, sharing its state with every consumer. Override to
    /// return a view that is isolated under <paramref name="scopeId"/> when the containerized service supports
    /// namespacing (databases, virtual hosts, key prefixes, ...).
    /// </summary>
    /// <param name="scopeId">A short, unique, lowercase identifier for the scope, safe to embed in names.</param>
    /// <returns>The scoped container view.</returns>
    public virtual ITestContainer CreateScope(string scopeId) => new TestContainerScope(this);
}
