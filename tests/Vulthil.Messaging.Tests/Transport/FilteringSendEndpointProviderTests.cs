using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class FilteringSendEndpointProviderTests : BaseUnitTestCase
{
    private static readonly Uri Address = new("queue:order-commands");

    private readonly ServiceProvider _services = new ServiceCollection()
        .AddTransient<IPublishFilter, StampingFilter>()
        .BuildServiceProvider();
    private readonly Lazy<FilteringSendEndpointProvider> _lazyTarget;

    private FilteringSendEndpointProvider Target => _lazyTarget.Value;

    public FilteringSendEndpointProviderTests()
    {
        Use<IServiceProvider>(_services);
        _lazyTarget = new(CreateInstance<FilteringSendEndpointProvider>);
    }

    protected override async ValueTask Dispose()
    {
        await _services.DisposeAsync();
        await base.Dispose();
    }

    [Fact]
    public async Task SendAsyncHandsTheTerminalTheMessageAndTheContextThatTheCallbackAndTheFiltersResolved()
    {
        // Arrange
        var message = new PlaceOrder("order-1");
        object? sentMessage = null;
        PublishContext? sentContext = null;
        var terminal = GetMock<ITransportSendEndpoint>();
        terminal.SetupGet(endpoint => endpoint.Address).Returns(Address);
        terminal
            .Setup(endpoint => endpoint.SendAsync(It.IsAny<object>(), It.IsAny<PublishContext>(), It.IsAny<CancellationToken>()))
            .Callback<object, PublishContext, CancellationToken>((sent, context, _) =>
            {
                sentMessage = sent;
                sentContext = context;
            })
            .Returns(Task.CompletedTask);
        GetMock<ITransportSendEndpointProvider>()
            .Setup(provider => provider.GetSendEndpointAsync(Address, It.IsAny<CancellationToken>()))
            .ReturnsAsync(terminal.Object);
        var endpoint = await Target.GetSendEndpointAsync(Address, CancellationToken);

        // Act
        await endpoint.SendAsync(message, context =>
        {
            context.SetCorrelationId("correlation-1");
            context.AddHeader("tenant", "acme");
            return ValueTask.CompletedTask;
        }, CancellationToken);

        // Assert
        endpoint.Address.ShouldBe(Address);
        sentMessage.ShouldBeSameAs(message);
        sentContext.ShouldNotBeNull();
        sentContext.MessageId.ShouldNotBeNullOrEmpty();
        sentContext.CorrelationId.ShouldBe("correlation-1");
        sentContext.Headers["tenant"].ShouldBe("acme");
        sentContext.Headers[StampingFilter.Header].ShouldBe(StampingFilter.Stamp);
    }

    public sealed record PlaceOrder(string OrderId);

    public sealed class StampingFilter : IPublishFilter
    {
        public const string Header = "stamped-by";
        public const string Stamp = "filter";

        public Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next)
        {
            context.Context.AddHeader(Header, Stamp);
            return next(context);
        }
    }
}
