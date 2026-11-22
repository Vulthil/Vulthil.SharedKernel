using Microsoft.Extensions.Hosting;
using Shouldly;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;
using Orders.Infrastructure.Messaging;

namespace Orders.Tests.Unit;

public sealed class SourceServiceHeaderPublishFilterTests : BaseUnitTestCase
{
    private readonly Lazy<SourceServiceHeaderPublishFilter> _target;

    public SourceServiceHeaderPublishFilterTests() => _target = new(CreateInstance<SourceServiceHeaderPublishFilter>);

    protected override ValueTask Initialize()
    {
        GetMock<IHostEnvironment>().SetupGet(environment => environment.ApplicationName).Returns("Orders");
        return base.Initialize();
    }

    [Fact]
    public async Task PublishAsync_stamps_the_application_name_and_calls_the_next_filter()
    {
        // Arrange
        var context = NewContext();
        var nextCalled = false;

        // Act
        await _target.Value.PublishAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.ShouldBeTrue();
        context.Context.Headers[SourceServiceHeaderPublishFilter.HeaderName].ShouldBe("Orders");
    }

    [Fact]
    public async Task PublishAsync_keeps_a_header_the_caller_set()
    {
        // Arrange
        var context = NewContext();
        context.Context.AddHeader(SourceServiceHeaderPublishFilter.HeaderName, "Backfill");

        // Act
        await _target.Value.PublishAsync(context, _ => Task.CompletedTask);

        // Assert
        context.Context.Headers[SourceServiceHeaderPublishFilter.HeaderName].ShouldBe("Backfill");
    }

    private static PublishFilterContext NewContext() => new()
    {
        Message = "payload",
        MessageType = typeof(string),
        Context = new PublishContext(),
        Kind = PublishKind.Publish,
    };
}
