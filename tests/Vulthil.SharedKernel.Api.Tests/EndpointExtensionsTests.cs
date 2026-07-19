using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Api.Tests;

public sealed class EndpointExtensionsTests : BaseUnitTestCase
{
    [Fact]
    public void AddEndpointsSkipsOpenGenericImplementations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEndpoints(typeof(EndpointExtensionsTests).Assembly);

        // Assert
        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(OpenGenericEndpoint<>));
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(ScopedDependentEndpoint));
    }

    [Fact]
    public void ScopedDependentEndpointConstructorThrowsOnNullMarker()
    {
        // Arrange

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ScopedDependentEndpoint(null!));
    }

    [Fact]
    public void MapEndpointsResolvesEndpointsFromAScopeNotTheRootProvider()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(options => options.ValidateScopes = true);
        builder.Services.AddScoped<ScopedMarker>();
        builder.Services.AddEndpoints(typeof(EndpointExtensionsTests).Assembly);
        using var app = builder.Build();

        // Act
        var exception = Record.Exception(() => app.MapEndpoints());

        // Assert
        Assert.Null(exception);
    }

    private sealed class ScopedMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class ScopedDependentEndpoint : IEndpoint
    {
        public ScopedDependentEndpoint(ScopedMarker marker)
        {
            ArgumentNullException.ThrowIfNull(marker);
        }

        public void MapEndpoint(IEndpointRouteBuilder app) => _ = app;
    }

    private sealed class OpenGenericEndpoint<T> : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            _ = app;
            _ = typeof(T);
        }
    }
}
