using Microsoft.Extensions.Logging;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Sending;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Represents the RabbitMqSendEndpointProviderTests.
/// </summary>
public sealed class RabbitMqSendEndpointProviderTests : BaseUnitTestCase
{
    private readonly Lazy<RabbitMqSendEndpointProvider> _lazyTarget;

    private RabbitMqSendEndpointProvider Target => _lazyTarget.Value;

    /// <summary>
    /// Initializes test infrastructure.
    /// </summary>
    public RabbitMqSendEndpointProviderTests()
    {
        var loggerFactoryMock = GetMock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(GetMock<ILogger>().Object);
        Use(loggerFactoryMock.Object);
        Use(GetMock<IInternalPublisher>().Object);
        Use(GetMock<IMessageConfigurationProvider>().Object);
        _lazyTarget = new(CreateInstance<RabbitMqSendEndpointProvider>);
    }

    /// <summary>
    /// Verifies that repeated lookups for the same Uri return the same cached endpoint instance.
    /// </summary>
    [Fact]
    public async Task GetSendEndpointAsyncShouldCacheByUri()
    {
        // Arrange
        var uri = new Uri("queue:my-queue");

        // Act
        var first = await Target.GetSendEndpointAsync(uri, CancellationToken);
        var second = await Target.GetSendEndpointAsync(uri, CancellationToken);

        // Assert
        first.ShouldBeSameAs(second);
        first.Address.ShouldBe(uri);
    }

    /// <summary>
    /// Verifies that different Uris yield distinct endpoint instances.
    /// </summary>
    [Fact]
    public async Task GetSendEndpointAsyncShouldReturnDistinctEndpointsForDistinctUris()
    {
        // Arrange
        var a = new Uri("queue:queue-a");
        var b = new Uri("queue:queue-b");

        // Act
        var endpointA = await Target.GetSendEndpointAsync(a, CancellationToken);
        var endpointB = await Target.GetSendEndpointAsync(b, CancellationToken);

        // Assert
        endpointA.ShouldNotBeSameAs(endpointB);
        endpointA.Address.ShouldBe(a);
        endpointB.Address.ShouldBe(b);
    }

    /// <summary>
    /// Verifies that an empty queue name in the Uri throws ArgumentException.
    /// </summary>
    [Fact]
    public async Task GetSendEndpointAsyncWithEmptyQueueNameThrows()
    {
        // Arrange
        var uri = new Uri("queue:");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await Target.GetSendEndpointAsync(uri, CancellationToken));
    }

    /// <summary>
    /// Verifies that NullSendEndpointProvider throws when asked for an endpoint.
    /// </summary>
    [Fact]
    public async Task NullSendEndpointProviderShouldThrow()
    {
        // Arrange
        var provider = NullSendEndpointProvider.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetSendEndpointAsync(new Uri("queue:any"), CancellationToken));
    }
}
