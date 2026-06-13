using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Filters;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Filters;

public sealed class DefaultFilterRegistrationTests : BaseUnitTestCase
{
    private static HostApplicationBuilder CreateHostBuilder() => Host.CreateApplicationBuilder();

    [Fact]
    public void LoggingFilterIsRegisteredByDefault()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(_ => { });

        // Assert
        var descriptors = builder.Services
            .Where(d => d.ServiceType == typeof(IConsumeFilter<>))
            .ToList();

        descriptors.ShouldHaveSingleItem();
        descriptors[0].ImplementationType.ShouldBe(typeof(LoggingConsumeFilter<>));
    }

    [Fact]
    public void LoggingFilterIsNotRegisteredWhenDisabledViaConfigureMessagingOptions()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act
        builder.AddMessaging(m =>
            m.ConfigureMessagingOptions(opts => opts.ConsumeFilters.EnableLogging = false));

        // Assert
        var descriptors = builder.Services
            .Where(d => d.ServiceType == typeof(IConsumeFilter<>))
            .ToList();

        descriptors.ShouldBeEmpty();
    }

    [Fact]
    public void LoggingFilterRegistersBeforeUserFiltersSoItStaysOutermost()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act — user adds an open-generic filter inside the configurator action.
        // The default must still appear first in the IConsumeFilter<> enumerable.
        builder.AddMessaging(m => m.AddOpenConsumeFilter(typeof(UserFilter<>)));

        // Assert
        var descriptors = builder.Services
            .Where(d => d.ServiceType == typeof(IConsumeFilter<>))
            .ToList();

        descriptors.Count.ShouldBe(2);
        descriptors[0].ImplementationType.ShouldBe(typeof(LoggingConsumeFilter<>));
        descriptors[1].ImplementationType.ShouldBe(typeof(UserFilter<>));
    }

    [Fact]
    public void AddConsumeFilterThrowsWhenTypeImplementsNoConsumeFilterInterface()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            builder.AddMessaging(m => m.AddConsumeFilter<NotAFilter>()));

        ex.Message.ShouldContain("must implement at least one");
    }

    [Fact]
    public void AddOpenConsumeFilterThrowsWhenTypeIsNotOpenGeneric()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            builder.AddMessaging(m => m.AddOpenConsumeFilter(typeof(NotAFilter))));

        ex.Message.ShouldContain("must be an open generic type");
    }

    [Fact]
    public void AddOpenConsumeFilterThrowsWhenOpenGenericDoesNotImplementConsumeFilter()
    {
        // Arrange
        var builder = CreateHostBuilder();

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            builder.AddMessaging(m => m.AddOpenConsumeFilter(typeof(NotAFilterOpen<>))));

        ex.Message.ShouldContain("must implement");
    }

    private sealed class UserFilter<TMessage> : IConsumeFilter<TMessage> where TMessage : notnull
    {
        public Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next) => next(context);
    }

    private sealed record NotAFilter(string Name);

    private sealed record NotAFilterOpen<TMessage>(TMessage Payload);
}
