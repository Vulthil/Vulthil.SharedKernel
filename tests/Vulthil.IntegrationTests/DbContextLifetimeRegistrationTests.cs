using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Pins that the services <c>AddDbContext</c> registers on top of a context take the lifetime of the context's
/// unkeyed registration, which is the one they resolve. These are registration-shape tests — no database is contacted.
/// </summary>
public sealed class DbContextLifetimeRegistrationTests : BaseUnitTestCase
{
    private const string ReportingKey = "reporting";

    [Fact]
    public void TheOutboxServicesIgnoreAKeyedRegistrationOfTheContext()
    {
        // Arrange
        var builder = NewBuilderWithScopedContextAndKeyedSingletonCopy();

        // Act
        builder.AddDbContext<LifetimeProbeDbContext>(database => database.EnableOutboxProcessing());

        // Assert
        Descriptor<IOutboxStore>(builder).Lifetime.ShouldBe(ServiceLifetime.Scoped);
        Descriptor<ISaveOutboxMessages>(builder).Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void TheContextInterfaceIgnoresAKeyedRegistrationOfTheContext()
    {
        // Arrange
        var builder = NewBuilderWithScopedContextAndKeyedSingletonCopy();

        // Act
        builder.AddDbContext<ILifetimeProbeDbContext, LifetimeProbeDbContext>(_ => { });

        // Assert
        Descriptor<ILifetimeProbeDbContext>(builder).Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    private static HostApplicationBuilder NewBuilderWithScopedContextAndKeyedSingletonCopy()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDbContext<LifetimeProbeDbContext>();
        builder.Services.AddKeyedSingleton<LifetimeProbeDbContext>(ReportingKey);
        return builder;
    }

    private static ServiceDescriptor Descriptor<TService>(HostApplicationBuilder builder) =>
        builder.Services.Single(descriptor => descriptor.ServiceType == typeof(TService) && !descriptor.IsKeyedService);

    internal interface ILifetimeProbeDbContext
    {
        DatabaseFacade Database { get; }
    }

    internal sealed class LifetimeProbeDbContext(DbContextOptions<LifetimeProbeDbContext> options)
        : BaseDbContext(options), ILifetimeProbeDbContext
    {
        protected override Assembly? ConfigurationAssembly => null;
    }
}
