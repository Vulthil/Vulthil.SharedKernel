using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.TestHarness.Tests;

public sealed class PublishFilterTests : BaseUnitTestCase
{
    [Fact]
    public async Task FilterObservesTheMessageAndItsPreMintedMessageId()
    {
        // Arrange
        using var host = BuildHost(messaging => messaging.AddPublishFilter<RecordingFilter>());
        var recorder = host.Services.GetRequiredService<FilterRecorder>();
        using var scope = host.Services.CreateScope();

        // Act
        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(new Ping("hi"), CancellationToken);

        // Assert
        var entry = recorder.Entries.ShouldHaveSingleItem();
        entry.Kind.ShouldBe(PublishKind.Publish);
        entry.MessageId.ShouldNotBeNullOrEmpty();
        entry.Message.ShouldBeOfType<Ping>().Value.ShouldBe("hi");
        host.Services.GetRequiredService<ITestHarness>().Published<Ping>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ShortCircuitingFilterSuppressesDelivery()
    {
        // Arrange
        using var host = BuildHost(messaging => messaging.AddPublishFilter<ShortCircuitFilter>());
        using var scope = host.Services.CreateScope();

        // Act
        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(new Ping("hi"), CancellationToken);

        // Assert
        host.Services.GetRequiredService<FilterRecorder>().Entries.ShouldHaveSingleItem();
        host.Services.GetRequiredService<ITestHarness>().Published<Ping>().ShouldBeEmpty();
    }

    [Fact]
    public async Task FiltersRunInRegistrationOrderOutermostFirst()
    {
        // Arrange
        using var host = BuildHost(messaging =>
        {
            messaging.AddPublishFilter<FirstOrderFilter>();
            messaging.AddPublishFilter<SecondOrderFilter>();
        });
        var recorder = host.Services.GetRequiredService<FilterRecorder>();
        using var scope = host.Services.CreateScope();

        // Act
        await scope.ServiceProvider.GetRequiredService<IPublisher>().PublishAsync(new Ping("hi"), CancellationToken);

        // Assert
        recorder.Order.Count.ShouldBe(2);
        recorder.Order[0].ShouldBe("first");
        recorder.Order[1].ShouldBe("second");
    }

    [Fact]
    public async Task SendIsObservedAsSendKindWithDestination()
    {
        // Arrange
        using var host = BuildHost(messaging => messaging.AddPublishFilter<RecordingFilter>());
        var recorder = host.Services.GetRequiredService<FilterRecorder>();
        using var scope = host.Services.CreateScope();
        var endpoint = await scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>()
            .GetSendEndpointAsync(new Uri("queue:pings"), CancellationToken);

        // Act
        await endpoint.SendAsync(new Ping("hi"), CancellationToken);

        // Assert
        var entry = recorder.Entries.ShouldHaveSingleItem();
        entry.Kind.ShouldBe(PublishKind.Send);
        entry.DestinationAddress.ShouldNotBeNull();
        entry.DestinationAddress.ToString().ShouldContain("pings");
    }

    private static IHost BuildHost(Action<IMessagingConfigurator> configureFilters)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<FilterRecorder>();
        builder.AddMessaging(messaging =>
        {
            configureFilters(messaging);
            messaging.UseTestHarness();
        });
        return builder.Build();
    }

    public sealed record Ping(string Value);

    public sealed class FilterRecorder
    {
        private readonly List<Entry> _entries = [];
        private readonly List<string> _order = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<Entry> Entries
        {
            get { lock (_gate) { return [.. _entries]; } }
        }

        public IReadOnlyList<string> Order
        {
            get { lock (_gate) { return [.. _order]; } }
        }

        public void Record(Entry entry)
        {
            lock (_gate) { _entries.Add(entry); }
        }

        public void RecordOrder(string id)
        {
            lock (_gate) { _order.Add(id); }
        }

        public sealed record Entry(object Message, PublishKind Kind, string? MessageId, Uri? DestinationAddress);
    }

    public sealed class RecordingFilter(FilterRecorder recorder) : IPublishFilter
    {
        public async Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next)
        {
            recorder.Record(new FilterRecorder.Entry(context.Message, context.Kind, context.Context.MessageId, context.DestinationAddress));
            await next(context);
        }
    }

    public sealed class ShortCircuitFilter(FilterRecorder recorder) : IPublishFilter
    {
        public Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next)
        {
            recorder.Record(new FilterRecorder.Entry(context.Message, context.Kind, context.Context.MessageId, context.DestinationAddress));
            return Task.CompletedTask;
        }
    }

    public sealed class FirstOrderFilter(FilterRecorder recorder) : IPublishFilter
    {
        public async Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next)
        {
            recorder.RecordOrder("first");
            await next(context);
        }
    }

    public sealed class SecondOrderFilter(FilterRecorder recorder) : IPublishFilter
    {
        public async Task PublishAsync(PublishFilterContext context, PublishFilterDelegate next)
        {
            recorder.RecordOrder("second");
            await next(context);
        }
    }
}
