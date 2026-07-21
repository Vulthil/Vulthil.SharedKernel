using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Api.Tests;

public sealed class DependencyInjectionTests : BaseUnitTestCase
{
    [Fact]
    public void AddOpenApiServicesWithNoArgumentsRegistersOpenApiServicesForTheDefaultDocumentName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOpenApiServices();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(DependencyInjection.DefaultDocumentName);

        // Assert
        Assert.Equal("v1", DependencyInjection.DefaultDocumentName);
        Assert.Equal(DependencyInjection.DefaultDocumentName, options.DocumentName);
    }

    [Fact]
    public void AddOpenApiServicesWithDocumentNameRegistersOpenApiServicesUnderThatName()
    {
        // Arrange
        var services = new ServiceCollection();
        const string documentName = "internal";

        // Act
        services.AddOpenApiServices(documentName);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(documentName);

        // Assert
        Assert.Equal(documentName, options.DocumentName);
    }

    [Fact]
    public void AddOpenApiServicesWithConfigureInvokesTheConfigureCallbackForTheNamedDocument()
    {
        // Arrange
        var services = new ServiceCollection();
        const string documentName = "internal";
        var configureCalled = false;

        // Act
        services.AddOpenApiServices(documentName, _ => configureCalled = true);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(documentName);

        // Assert
        Assert.True(configureCalled);
        Assert.Equal(documentName, options.DocumentName);
    }

    [Fact]
    public void AddOpenApiServicesWithConfigureThrowsOnNullConfigure()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddOpenApiServices("v1", null!));
    }

    [Fact]
    public void MapOpenApiEndpointsMapsTheOpenApiDocumentRouteAndReturnsAConventionBuilder()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddOpenApiServices();
        using var app = builder.Build();

        // Act
        var conventionBuilder = app.MapOpenApiEndpoints();

        // Assert
        Assert.NotNull(conventionBuilder);
        IEndpointRouteBuilder endpointRouteBuilder = app;
        var routePatterns = endpointRouteBuilder.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();
        Assert.Contains(routePatterns, pattern => pattern != null && pattern.Contains("openapi", StringComparison.OrdinalIgnoreCase));
    }
}
