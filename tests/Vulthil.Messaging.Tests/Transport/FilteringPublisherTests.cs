using Microsoft.Extensions.DependencyInjection;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class FilteringPublisherTests : BaseUnitTestCase
{
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddTransient<IPublishFilter, StampingFilter>()
        .BuildServiceProvider();
    private readonly Lazy<FilteringPublisher> _lazyTarget;

    private FilteringPublisher Target => _lazyTarget.Value;

    public FilteringPublisherTests()
    {
        Use<IServiceProvider>(_services);
        _lazyTarget = new(CreateInstance<FilteringPublisher>);
    }

    protected override async ValueTask Dispose()
    {
        await _services.DisposeAsync();
        await base.Dispose();
    }

    [Fact]
    public async Task PublishAsyncHandsTheTerminalTheMessageAndTheContextThatTheCallbackAndTheFiltersResolved()
    {
        // Arrange
        var message = new OrderPlaced("order-1");
        object? publishedMessage = null;
        PublishContext? publishedContext = null;
        GetMock<ITransportPublisher>()
            .Setup(transport => transport.PublishAsync(It.IsAny<object>(), It.IsAny<PublishContext>(), It.IsAny<CancellationToken>()))
            .Callback<object, PublishContext, CancellationToken>((published, context, _) =>
            {
                publishedMessage = published;
                publishedContext = context;
            })
            .Returns(Task.CompletedTask);

        // Act
        await Target.PublishAsync(message, context =>
        {
            context.SetCorrelationId("correlation-1");
            context.AddHeader("tenant", "acme");
            return ValueTask.CompletedTask;
        }, CancellationToken);

        // Assert
        publishedMessage.ShouldBeSameAs(message);
        publishedContext.ShouldNotBeNull();
        publishedContext.MessageId.ShouldNotBeNullOrEmpty();
        publishedContext.CorrelationId.ShouldBe("correlation-1");
        publishedContext.Headers["tenant"].ShouldBe("acme");
        publishedContext.Headers[StampingFilter.Header].ShouldBe(StampingFilter.Stamp);
    }

    public sealed record OrderPlaced(string OrderId);

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
