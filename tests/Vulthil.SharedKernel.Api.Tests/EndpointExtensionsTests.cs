using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public void MapEndpointsMapsEachDiscoveredEndpointThroughTheProvidedRouteGroup()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<ScopedMarker>();
        builder.Services.AddEndpoints(typeof(EndpointExtensionsTests).Assembly);
        using var app = builder.Build();
        var group = app.MapGroup("scoped-group");

        // Act
        var result = app.MapEndpoints(group);

        // Assert
        Assert.Same(app, result);
        Assert.Contains(builder.Services, descriptor => descriptor.ImplementationType == typeof(RecordingEndpoint));
        IEndpointRouteBuilder endpointRouteBuilder = app;
        var routePatterns = endpointRouteBuilder.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();
        Assert.Contains("scoped-group/recording-endpoint", routePatterns);
    }

    [Fact]
    public void MapEndpointsThrowsOnNullApp()
    {
        // Arrange

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => EndpointExtensions.MapEndpoints(null!));
    }

    [Fact]
    public void AddEndpointsThrowsOnNullServices()
    {
        // Arrange

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => EndpointExtensions.AddEndpoints(null!, typeof(EndpointExtensionsTests).Assembly));
    }

    [Fact]
    public void AddEndpointsThrowsOnNullAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddEndpoints(null!));
    }

    private sealed class RecordingEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) => app.MapGet("recording-endpoint", () => TypedResults.Ok());
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
